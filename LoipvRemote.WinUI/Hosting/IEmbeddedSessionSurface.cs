using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.WinUI.Hosting;

/// <summary>Desktop shell boundary for a single native protocol session surface.</summary>
public interface IEmbeddedSessionSurface
{
    IntPtr Handle { get; }
    void EnsureHostWindow();
    void SetVisible(bool visible);
    bool Attach(IEmbeddedWindow session, TimeSpan timeout);
    void Focus();
}
