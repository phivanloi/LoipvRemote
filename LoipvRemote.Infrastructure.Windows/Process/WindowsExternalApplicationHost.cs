using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Infrastructure.Windows.ProcessManagement;

/// <summary>Windows adapter that owns an external application process.</summary>
public sealed class WindowsExternalApplicationHost : IExternalApplicationHost
{
    private System.Diagnostics.Process? _process;

    public bool IsRunning => _process is { HasExited: false };
    public IntPtr WindowHandle => IsRunning ? _process!.MainWindowHandle : IntPtr.Zero;
    public string WindowTitle => IsRunning ? _process!.MainWindowTitle : string.Empty;

    public event EventHandler? Exited;

    public bool Start(ExternalApplicationDefinition definition)
    {
        if (IsRunning)
            return false;

        DisposeProcess();
        _process = new System.Diagnostics.Process
        {
            StartInfo = ExternalApplicationProcessStartInfoFactory.Create(definition),
            EnableRaisingEvents = true
        };
        _process.Exited += ProcessOnExited;

        if (_process.Start())
            return true;

        DisposeProcess();
        return false;
    }

    public bool WaitForMainWindow(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (IsRunning && stopwatch.Elapsed <= timeout)
        {
            _process!.Refresh();
            if (WindowHandle != IntPtr.Zero && WindowTitle != "Default IME")
                return true;

            Thread.Sleep(15);
        }

        return false;
    }

    public bool AttachTo(IntPtr parentWindowHandle)
    {
        if (parentWindowHandle == IntPtr.Zero || WindowHandle == IntPtr.Zero)
            return false;

        SetParent(WindowHandle, parentWindowHandle);
        return GetParent(WindowHandle) == parentWindowHandle;
    }

    public void Resize(EmbeddedWindowBounds bounds)
    {
        if (!bounds.IsValid || WindowHandle == IntPtr.Zero)
            return;

        MoveWindow(WindowHandle, bounds.X, bounds.Y, bounds.Width, bounds.Height, true);
    }

    public void Focus()
    {
        if (WindowHandle == IntPtr.Zero)
            return;

        SetForegroundWindow(WindowHandle);
    }

    public void Close()
    {
        if (!IsRunning)
            return;

        _process!.Kill(entireProcessTree: true);
        _process.WaitForExit();
    }

    public void Dispose() => DisposeProcess();

    private void DisposeProcess()
    {
        if (_process is not null)
        {
            _process.Exited -= ProcessOnExited;
            _process.Dispose();
        }

        _process = null;
    }

    private void ProcessOnExited(object? sender, EventArgs e) => Exited?.Invoke(this, EventArgs.Empty);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr childWindowHandle, IntPtr parentWindowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetParent(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr windowHandle, int x, int y, int width, int height, bool repaint);
}
