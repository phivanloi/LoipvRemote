using LoipvRemote.Domain.Protocols.Rdp;
using LoipvRemote.Protocols.Rdp;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

public sealed class RdpVersionSelectorTests
{
    [Test]
    public void SelectsTheHighestSupportedGeneration()
    {
        RdpVersion selected = RdpVersionSelector.SelectHighestSupported(version => version is RdpVersion.Rdc7 or RdpVersion.Rdc10);

        Assert.That(selected, Is.EqualTo(RdpVersion.Rdc10));
    }

    [Test]
    public void ListsSupportedGenerationsInStableAscendingOrder()
    {
        IReadOnlyList<RdpVersion> versions = RdpVersionSelector.GetSupportedVersions(version => version is RdpVersion.Rdc6 or RdpVersion.Rdc9);

        Assert.That(versions, Is.EqualTo(new[] { RdpVersion.Rdc6, RdpVersion.Rdc9 }));
    }
}
