using LoipvRemote.Domain.Connections;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Core;

public sealed class SupportedProtocolScopeTests
{
    [Test]
    public void ProtocolCatalogContainsOnlyTheSupportedRemoteProtocols()
    {
        ProtocolKind[] protocols = Enum.GetValues<ProtocolKind>();

        Assert.That(protocols, Is.EquivalentTo(new[]
        {
            ProtocolKind.Ssh2,
            ProtocolKind.Rdp,
            ProtocolKind.Vnc
        }));
    }
}
