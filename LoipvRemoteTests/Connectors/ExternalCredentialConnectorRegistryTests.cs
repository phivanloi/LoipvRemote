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
}
