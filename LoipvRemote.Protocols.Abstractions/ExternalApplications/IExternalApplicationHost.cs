using LoipvRemote.Domain.Connections;

namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Platform adapter that owns the lifecycle of an external application window.</summary>
public interface IExternalApplicationHost : IDisposable
{
    bool IsRunning { get; }
    IntPtr WindowHandle { get; }
    string WindowTitle { get; }

    event EventHandler? Exited;

    bool Start(ExternalApplicationDefinition definition);
    bool WaitForMainWindow(TimeSpan timeout);
    bool AttachTo(IntPtr parentWindowHandle);
    void Resize(EmbeddedWindowBounds bounds);
    void Focus();
    void Close();
}
