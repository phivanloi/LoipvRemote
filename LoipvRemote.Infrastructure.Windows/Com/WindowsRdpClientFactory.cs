using LoipvRemote.Domain.Protocols.Rdp;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Rdp;

namespace LoipvRemote.Infrastructure.Windows.Com;

/// <summary>
/// Windows-specific RDP client construction. The WinUI shell receives this
/// through <see cref="IRdpClientFactory"/> and never creates an ActiveX host.
/// </summary>
public sealed class WindowsRdpClientFactory : IRdpClientFactory
{
    public IRdpClient Create(RdpVersion requestedVersion)
    {
        RdpVersion version = requestedVersion == RdpVersion.Highest
            ? RdpVersionSelector.SelectHighestSupported(RdpActiveXRuntime.IsSupported)
            : requestedVersion;
        return new RdpActiveXRuntime(version);
    }
}
