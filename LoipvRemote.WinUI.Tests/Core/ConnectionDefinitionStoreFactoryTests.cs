using LoipvRemote.Infrastructure.Persistence;
using LoipvRemote.Infrastructure.Persistence.SqlServer;
using LoipvRemote.Infrastructure.Persistence.Xml;
using LoipvRemote.Application.Configuration;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Core;

public sealed class ConnectionDefinitionStoreFactoryTests
{
    [TestCase(ConnectionDefinitionStoreKind.Xml, typeof(XmlConnectionDefinitionStore))]
    [TestCase(ConnectionDefinitionStoreKind.SqlServer, typeof(SqlServerConnectionDefinitionStore))]
    public void CreatesTheRequestedBackend(ConnectionDefinitionStoreKind kind, Type expectedType)
    {
        IConnectionDefinitionStore store = new ConnectionDefinitionStoreFactory()
            .Create(new ConnectionDefinitionStoreOptions(kind, "test-location"));

        Assert.That(store, Is.TypeOf(expectedType));
    }

    [Test]
    public void RejectsMissingStoreLocation()
    {
        Assert.That(
            () => new ConnectionDefinitionStoreFactory().Create(
                new ConnectionDefinitionStoreOptions(ConnectionDefinitionStoreKind.Xml, " ")),
            Throws.ArgumentException);
    }
}
