using LoipvRemote.Infrastructure.Persistence;
using LoipvRemote.Infrastructure.Persistence.MySql;
using LoipvRemote.Infrastructure.Persistence.Odbc;
using LoipvRemote.Infrastructure.Persistence.Sqlite;
using LoipvRemote.Infrastructure.Persistence.SqlServer;
using LoipvRemote.Infrastructure.Persistence.Xml;
using LoipvRemote.UseCases.Configuration;
using NUnit.Framework;

namespace LoipvRemoteTests.Infrastructure.Persistence;

public sealed class ConnectionDefinitionStoreFactoryTests
{
    [TestCase(ConnectionDefinitionStoreKind.Xml, typeof(XmlConnectionDefinitionStore))]
    [TestCase(ConnectionDefinitionStoreKind.Sqlite, typeof(SqliteConnectionDefinitionStore))]
    [TestCase(ConnectionDefinitionStoreKind.SqlServer, typeof(SqlServerConnectionDefinitionStore))]
    [TestCase(ConnectionDefinitionStoreKind.MySql, typeof(MySqlConnectionDefinitionStore))]
    [TestCase(ConnectionDefinitionStoreKind.Odbc, typeof(OdbcConnectionDefinitionStore))]
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
            () => new ConnectionDefinitionStoreFactory().Create(new ConnectionDefinitionStoreOptions(ConnectionDefinitionStoreKind.Xml, " ")),
            Throws.ArgumentException);
    }
}
