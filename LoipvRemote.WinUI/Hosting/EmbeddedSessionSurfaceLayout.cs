using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.WinUI.Hosting;

/// <summary>Separates top-level placement from the embedded protocol's local bounds.</summary>
public static class EmbeddedSessionSurfaceLayout
{
    public static EmbeddedWindowBounds ToProtocolBounds(EmbeddedWindowBounds hostBounds) =>
        new(0, 0, hostBounds.Width, hostBounds.Height);
}
