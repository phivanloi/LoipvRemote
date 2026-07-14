using LoipvRemote.Connection.Protocol;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemoteTests.TestHelpers;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

public class PuttyProtocolCapabilitiesTests
{
    [Test]
    public void ExposesItsSupportedEmbeddedSessionCapabilities()
    {
        var capabilities = new PuttyBase(new ExternalCredentialConnectorRegistry([]), TestSecretStore.Instance).Capabilities;

        Assert.Multiple(() =>
        {
            Assert.That(capabilities.HasFlag(ProtocolCapabilities.EmbeddedWindow), Is.True);
            Assert.That(capabilities.HasFlag(ProtocolCapabilities.Resize), Is.True);
            Assert.That(capabilities.HasFlag(ProtocolCapabilities.Reconnect), Is.True);
            Assert.That(capabilities.HasFlag(ProtocolCapabilities.CredentialInjection), Is.True);
        });
    }

    [Test]
    public void ImplementsTheEmbeddedWindowBoundary()
    {
        IEmbeddedWindow embeddedWindow = new PuttyBase(new ExternalCredentialConnectorRegistry([]), TestSecretStore.Instance);

        Assert.That(embeddedWindow.IsAvailable, Is.False);
    }
}
