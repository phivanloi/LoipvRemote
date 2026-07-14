using System.Diagnostics;
using LoipvRemote.Domain.Protocols;

namespace LoipvRemote.Protocols.Putty;

/// <summary>Owns the child PuTTY process lifecycle independently of the desktop host.</summary>
public sealed class PuttyProcessSession : IDisposable
{
    private Process? Process { get; set; }

    public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;

    public bool IsRunning => Process?.HasExited == false;
    public bool HasExited => Process?.HasExited != false;
    public nint ProcessHandle => Process is { HasExited: false } process ? process.Handle : 0;
    public nint MainWindowHandle => Process?.MainWindowHandle ?? 0;
    public string MainWindowTitle => Process?.MainWindowTitle ?? string.Empty;

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
            WindowStyle = options.StartMinimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal
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

        try
        {
            if (!Process.HasExited)
                Process.Kill();
        }
        finally
        {
            Process.Dispose();
            Process = null;
            State = ProtocolSessionState.Closed;
        }
    }

    public void Dispose() => Stop();

    public void Refresh() => Process?.Refresh();
}
