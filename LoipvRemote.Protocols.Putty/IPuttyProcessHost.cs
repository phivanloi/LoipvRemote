namespace LoipvRemote.Protocols.Putty;

/// <summary>Process boundary used by the PuTTY protocol session.</summary>
public interface IPuttyProcessHost : IDisposable
{
    bool IsRunning { get; }
    int ProcessId { get; }
    nint MainWindowHandle { get; }
    string MainWindowTitle { get; }
    bool Start(PuttyProcessStartOptions options, EventHandler exited);
    void StopProcess();
    void Refresh();
}
