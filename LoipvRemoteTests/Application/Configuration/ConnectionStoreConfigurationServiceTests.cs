using System;
using System.Threading.Tasks;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.UseCases.Configuration;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.UseCases.Configuration;

public class ConnectionStoreConfigurationServiceTests
{
    [Test]
    public async Task ValidatesTreeBeforeWritingToStore()
    {
        IConnectionDefinitionStore store = Substitute.For<IConnectionDefinitionStore>();
        IConnectionDefinitionStoreFactory factory = Substitute.For<IConnectionDefinitionStoreFactory>();
        ConnectionDefinitionStoreOptions options = new(ConnectionDefinitionStoreKind.Xml, "connections.xml");
        factory.Create(options).Returns(store);
        var service = new ConnectionStoreConfigurationService(factory);
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "ssh", "host.example", 22, ProtocolKind.Ssh2, CredentialReference.None);

        var tree = new ConnectionTreeDefinition([], [definition]);
        await service.SaveAsync(options, tree);

        factory.Received(1).Create(options);
        await store.Received(1).SaveAsync(
            Arg.Is<ConnectionTreeDefinition>(value => value.Equals(tree)),
            Arg.Any<System.Threading.CancellationToken>());
    }

    [Test]
    public void RejectsInvalidTreesBeforeWritingToStore()
    {
        IConnectionDefinitionStore store = Substitute.For<IConnectionDefinitionStore>();
        IConnectionDefinitionStoreFactory factory = Substitute.For<IConnectionDefinitionStoreFactory>();
        ConnectionDefinitionStoreOptions options = new(ConnectionDefinitionStoreKind.Xml, "connections.xml");
        factory.Create(options).Returns(store);
        var service = new ConnectionStoreConfigurationService(factory);
        var invalidDefinition = new ConnectionDefinition(
            Guid.NewGuid(), string.Empty, "host.example", 22, ProtocolKind.Ssh2, CredentialReference.None);

        Assert.That(
            async () => await service.SaveAsync(options, new ConnectionTreeDefinition([], [invalidDefinition])),
            Throws.ArgumentException);
        factory.DidNotReceive().Create(Arg.Any<ConnectionDefinitionStoreOptions>());
        store.DidNotReceive().SaveAsync(Arg.Any<ConnectionTreeDefinition>(), Arg.Any<System.Threading.CancellationToken>());
    }
}
