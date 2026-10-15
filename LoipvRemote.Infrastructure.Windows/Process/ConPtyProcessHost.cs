using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LoipvRemote.Infrastructure.Windows.Process;

/// <summary>Hosts one character-mode process through the Windows pseudoconsole API.</summary>
[SupportedOSPlatform("windows10.0.17763")]
public sealed class ConPtyProcessHost : IAsyncDisposable
{
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const int ProcThreadAttributePseudoConsole = 0x00020016;
    private const int ErrorBrokenPipe = 109;
    private const int ErrorOperationAborted = 995;
    private readonly SemaphoreSlim _inputGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _pseudoConsoleGate = new();
    private StreamWriter? _input;
    private SafeFileHandle? _outputRead;
    private System.Diagnostics.Process? _process;
    private Task? _outputPump;
    private IntPtr _pseudoConsole;
    private string? _processName;
    private bool _disposed;

    public event Action<string>? OutputReceived;
    public event Action? Exited;

    public bool IsRunning => _process is { HasExited: false };
    public int? ExitCode => _process is { HasExited: true } process ? process.ExitCode : null;
    public int? ProcessId => _process?.Id;
    public string? ProcessName => _processName;

    public void Start(
        string executablePath,
        string arguments,
        string workingDirectory,
        short columns,
        short rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_process is not null)
            throw new InvalidOperationException("The pseudoconsole process has already been started.");
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("The terminal executable does not exist.", executablePath);
        _processName = Path.GetFileNameWithoutExtension(executablePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, (short)2);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, (short)1);

        SafeFileHandle? inputRead = null;
        SafeFileHandle? inputWrite = null;
        SafeFileHandle? outputRead = null;
        SafeFileHandle? outputWrite = null;
        IntPtr attributeList = IntPtr.Zero;
        try
        {
            CreatePipePair(out inputRead, out inputWrite);
            CreatePipePair(out outputRead, out outputWrite);

            int createResult = CreatePseudoConsole(
                new Coordinate(columns, rows),
                inputRead.DangerousGetHandle(),
                outputWrite.DangerousGetHandle(),
                0,
                out _pseudoConsole);
            if (createResult < 0)
                Marshal.ThrowExceptionForHR(createResult);

            nuint attributeListSize = 0;
            _ = InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeListSize);
            attributeList = Marshal.AllocHGlobal(checked((int)attributeListSize));
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    (IntPtr)ProcThreadAttributePseudoConsole,
                    _pseudoConsole,
                    (nuint)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var startup = new StartupInfoEx
            {
                StartupInfo = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>() },
                AttributeList = attributeList
            };
            var commandLine = new StringBuilder(string.IsNullOrWhiteSpace(arguments)
                ? $"\"{executablePath}\""
                : $"\"{executablePath}\" {arguments}");
            var processSecurity = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>() };
            var threadSecurity = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>() };
            if (!CreateProcess(
                    null,
                    commandLine,
                    ref processSecurity,
                    ref threadSecurity,
                    false,
                    ExtendedStartupInfoPresent,
                    IntPtr.Zero,
                    workingDirectory,
                    ref startup,
                    out ProcessInformation processInformation))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                WindowsJobObjectProcessTracker.AddProcessHandle(processInformation.Process);
                _process = System.Diagnostics.Process.GetProcessById((int)processInformation.ProcessId);
                _process.EnableRaisingEvents = true;
                _process.Exited += ProcessOnExited;
            }
            finally
            {
                CloseHandle(processInformation.Thread);
                CloseHandle(processInformation.Process);
            }

            _input = new StreamWriter(
                new FileStream(inputWrite, FileAccess.Write, 4096, isAsync: false),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };
            inputWrite = null;
            _outputRead = outputRead;
            outputRead = null;
            // CreatePipe produces synchronous handles. Keep the blocking read
            // away from both the WinUI thread and the input writer.
            _outputPump = Task.Run(() => PumpOutputAsync(_lifetime.Token));
        }
        catch
        {
            ClosePseudoConsoleIfOpen();
            throw;
        }
        finally
        {
            if (attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }
            inputRead?.Dispose();
            inputWrite?.Dispose();
            outputRead?.Dispose();
            outputWrite?.Dispose();
        }
    }

    public async ValueTask WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(data);
        if (_input is null)
            throw new InvalidOperationException("The pseudoconsole process has not been started.");

        await _inputGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _input.WriteAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _input.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _inputGate.Release();
        }
    }

    public void Resize(short columns, short rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (columns < 2 || rows < 1)
            return;

        int result;
        lock (_pseudoConsoleGate)
        {
            if (_pseudoConsole == IntPtr.Zero)
                return;
            result = ResizePseudoConsole(_pseudoConsole, new Coordinate(columns, rows));
        }
        if (result < 0)
            Marshal.ThrowExceptionForHR(result);
    }

    private Task PumpOutputAsync(CancellationToken cancellationToken)
    {
        if (_outputRead is null)
            return Task.CompletedTask;
        SafeFileHandle outputRead = _outputRead;

        byte[] bytes = new byte[4096];
        char[] characters = new char[4096];
        Decoder decoder = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: false).GetDecoder();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!ReadFile(outputRead, bytes, 1, out int read, IntPtr.Zero))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error is ErrorBrokenPipe or ErrorOperationAborted)
                        break;
                    throw new Win32Exception(error);
                }
                if (read == 0)
                    break;
                PublishDecodedOutput(decoder, bytes, read, characters);

                if (PeekNamedPipe(
                        outputRead,
                        IntPtr.Zero,
                        0,
                        IntPtr.Zero,
                        out int available,
                        IntPtr.Zero) &&
                    available > 0)
                {
                    int requested = Math.Min(available, bytes.Length);
                    if (ReadFile(outputRead, bytes, requested, out read, IntPtr.Zero) && read > 0)
                        PublishDecodedOutput(decoder, bytes, read, characters);
                }
            }
        }
        catch (Exception exception) when (
            cancellationToken.IsCancellationRequested &&
            exception is IOException or ObjectDisposedException)
        {
            // Normal pseudoconsole teardown closes the output pipe.
        }

        return Task.CompletedTask;
    }

    private void PublishDecodedOutput(Decoder decoder, byte[] bytes, int count, char[] characters)
    {
        decoder.Convert(
            bytes,
            0,
            count,
            characters,
            0,
            characters.Length,
            flush: false,
            out _,
            out int charactersUsed,
            out _);
        if (charactersUsed > 0)
            OutputReceived?.Invoke(new string(characters, 0, charactersUsed));
    }

    private void ProcessOnExited(object? sender, EventArgs args)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(150).ConfigureAwait(false);
            ClosePseudoConsoleIfOpen();
        });
        Exited?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        _lifetime.Cancel();
        _input?.Dispose();
        _input = null;
        ClosePseudoConsoleIfOpen();
        _outputRead?.Dispose();
        _outputRead = null;

        if (_outputPump is not null)
        {
            try
            {
                await _outputPump.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is TimeoutException or OperationCanceledException or IOException or ObjectDisposedException)
            {
                // The child is already being torn down; do not block app shutdown.
            }
        }

        if (_process is not null)
        {
            _process.Exited -= ProcessOnExited;
            if (!_process.HasExited)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The process exited between the HasExited check and Kill.
                }
            }
            _process.Dispose();
            _process = null;
        }

        _inputGate.Dispose();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ClosePseudoConsoleIfOpen()
    {
        lock (_pseudoConsoleGate)
        {
            if (_pseudoConsole == IntPtr.Zero)
                return;
            ClosePseudoConsole(_pseudoConsole);
            _pseudoConsole = IntPtr.Zero;
        }
    }

    private static void CreatePipePair(out SafeFileHandle read, out SafeFileHandle write)
    {
        if (!CreatePipe(out IntPtr readHandle, out IntPtr writeHandle, IntPtr.Zero, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        read = new SafeFileHandle(readHandle, ownsHandle: true);
        write = new SafeFileHandle(writeHandle, ownsHandle: true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Coordinate(short x, short y)
    {
        public readonly short X = x;
        public readonly short Y = y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2Size;
        public IntPtr Reserved2;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ProcessInformation
    {
        public readonly IntPtr Process;
        public readonly IntPtr Thread;
        public readonly uint ProcessId;
        public readonly uint ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool InheritHandle;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreatePipe(
        out IntPtr readPipe,
        out IntPtr writePipe,
        IntPtr pipeAttributes,
        int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(
        SafeFileHandle file,
        byte[] buffer,
        int bytesToRead,
        out int bytesRead,
        IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekNamedPipe(
        SafeFileHandle pipe,
        IntPtr buffer,
        int bufferSize,
        IntPtr bytesRead,
        out int totalBytesAvailable,
        IntPtr bytesLeftThisMessage);

    [DllImport("kernel32.dll")]
    private static extern int CreatePseudoConsole(
        Coordinate size,
        IntPtr input,
        IntPtr output,
        uint flags,
        out IntPtr pseudoConsole);

    [DllImport("kernel32.dll")]
    private static extern int ResizePseudoConsole(IntPtr pseudoConsole, Coordinate size);

    [DllImport("kernel32.dll")]
    private static extern void ClosePseudoConsole(IntPtr pseudoConsole);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr attributeList,
        int attributeCount,
        int flags,
        ref nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr attributeList,
        uint flags,
        IntPtr attribute,
        IntPtr value,
        nuint size,
        IntPtr previousValue,
        IntPtr returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

#pragma warning disable CA1838 // CreateProcessW requires a mutable command-line buffer.
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(
        string? applicationName,
        StringBuilder commandLine,
        ref SecurityAttributes processAttributes,
        ref SecurityAttributes threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);
#pragma warning restore CA1838

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
