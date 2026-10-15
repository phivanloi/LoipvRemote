using System.Diagnostics;
using LoipvRemote.Domain.Protocols;

namespace LoipvRemote.Protocols.Putty;

/// <summary>Owns the child PuTTY process lifecycle independently of the desktop host.</summary>
public sealed class PuttyProcessSession(Action<Process>? processStarted = null) : IPuttyProcessHost
{
    private readonly Action<Process>? _processStarted = processStarted;
    private Process? Process { get; set; }

    public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;

    public bool IsRunning => Process?.HasExited == false;
    public int ProcessId => Process is { HasExited: false } process ? process.Id : 0;
    public bool HasExited => Process?.HasExited != false;
    public nint ProcessHandle => Process is { HasExited: false } process ? process.Handle : 0;
    public nint MainWindowHandle
    {
        get
        {
            Process?.Refresh();
            return Process?.MainWindowHandle ?? 0;
        }
    }

    public string MainWindowTitle
    {
        get
        {
            Process?.Refresh();
            return Process?.MainWindowTitle ?? string.Empty;
        }
    }

    public bool Start(PuttyProcessStartOptions options, EventHandler exited)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(exited);
        if (State is not ProtocolSessionState.Created and not ProtocolSessionState.Closed)
            return false;

        ProcessStartInfo startInfo = new(options.ExecutablePath)
        {
            Arguments = options.Arguments,
            UseShellExecute = false,
            WindowStyle = options.StartHidden
                ? ProcessWindowStyle.Hidden
                : options.StartMinimized
                    ? ProcessWindowStyle.Minimized
                    : ProcessWindowStyle.Normal
        };
        Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Exited += exited;
        if (!process.Start())
        {
            process.Exited -= exited;
            process.Dispose();
            State = ProtocolSessionState.Faulted;
            return false;
        }

        Process = process;
        _processStarted?.Invoke(process);
        try
        {
            // PuTTY creates its top-level window asynchronously. Waiting for the
            // GUI input queue and refreshing the process snapshot prevents the
            // first tab activation from observing a zero window handle.
            process.WaitForInputIdle(5000);
        }
        catch (InvalidOperationException)
        {
            // The process may exit before creating a GUI queue; IsRunning and the
            // next attachment retry remain the source of truth.
        }
        process.Refresh();
        State = ProtocolSessionState.Connected;
        return true;
    }

    public void Stop()
    {
        if (Process is null)
        {
            State = ProtocolSessionState.Closed;
            return;
        }

        bool exited = true;
        try
        {
            if (!Process.HasExited)
            {
                // PuTTY can create a helper child process while opening a
                // session. Killing only the top-level window leaves that
                // helper orphaned after LoipvRemote exits. Terminate the
                // complete process tree and wait briefly so shutdown can
                // verify that no protocol child remains alive.
                Process.Kill(entireProcessTree: true);
                exited = Process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and Kill/WaitForExit.
        }

        if (!exited)
        {
            State = ProtocolSessionState.Closing;
            throw new TimeoutException("PuTTY and its child processes did not exit within 5 seconds.");
        }

        Process.Dispose();
        Process = null;
        State = ProtocolSessionState.Closed;
    }

    public void StopProcess() => Stop();

    public void Dispose() => Stop();

    public void Refresh() => Process?.Refresh();
}
