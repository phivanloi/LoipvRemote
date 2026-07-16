using System;
using LoipvRemote.Connectors.Abstractions;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.Connectors;

public class ExternalCredentialConnectorRegistryTests
{
    [Test]
    public void ResolvesConnectorByProviderNameWithoutCaseSensitivity()
    {
        IExternalCredentialConnector connector = Substitute.For<IExternalCredentialConnector>();
        connector.Provider.Returns("OnePassword");
        var registry = new ExternalCredentialConnectorRegistry([connector]);

        Assert.That(registry.GetRequired("onepassword"), Is.SameAs(connector));
    }

    [Test]
    public void RejectsAnUnregisteredProvider()
    {
        var registry = new ExternalCredentialConnectorRegistry([]);

        Assert.That(() => registry.GetRequired("missing"), Throws.InstanceOf<NotSupportedException>());
    }

    [Test]
    public async Task ResolvesCredentialsThroughTheAsyncContract()
    {
        IExternalCredentialConnector connector = Substitute.For<IExternalCredentialConnector>();
        connector.Provider.Returns("OnePassword");
        ExternalCredential expected = new("user", "password", "domain", "private-key");
        connector.ResolveAsync("reference", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));
        var registry = new ExternalCredentialConnectorRegistry([connector]);

        ExternalCredential actual = await registry.ResolveAsync(
            "onepassword",
            new ExternalCredentialRequest(
                "reference", "user", "host", "", "", 0, ExternalCredentialProtocol.Ssh));

        Assert.That(actual, Is.EqualTo(expected));
        await connector.Received(1).ResolveAsync("reference", Arg.Any<CancellationToken>());
    }
}
