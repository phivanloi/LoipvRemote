using VncSharpCore;

namespace LoipvRemote.Protocols.Vnc;

/// <summary>VncSharp control adapter owned by the VNC protocol module.</summary>
public sealed class VncSharpClientAdapter : IVncClient
{
    private readonly RemoteDesktop _remoteDesktop;

    public VncSharpClientAdapter(RemoteDesktop remoteDesktop)
    {
        _remoteDesktop = remoteDesktop ?? throw new ArgumentNullException(nameof(remoteDesktop));
    }

    public void SetPort(int port) => _remoteDesktop.VncPort = port;

    public void Connect(string host, bool viewOnly, bool smartSize) =>
        _remoteDesktop.Connect(host, viewOnly, smartSize);

    public void Disconnect() => _remoteDesktop.Disconnect();
}
