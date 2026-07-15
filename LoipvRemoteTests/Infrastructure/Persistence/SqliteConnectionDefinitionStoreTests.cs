using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace LoipvRemoteTests.Infrastructure.Persistence;

public class SqliteConnectionDefinitionStoreTests
{
    [Test]
    public async Task RoundTripsConnectionDefinitionsIncludingExternalApplications()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"loipvremote-{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteConnectionDefinitionStore($"Data Source={databasePath};Pooling=False");
            Guid folderId = Guid.NewGuid();
            var options = new ConnectionNodeOptions(
                new Dictionary<string, string> { ["PuttySession"] = "production" },
                ["PuttySession"]);
            var tree = new ConnectionTreeDefinition(
            [
                new ConnectionFolderDefinition(folderId, "Production", SortOrder: 1, Options: options, IsRoot: true)
            ],
            [
                new ConnectionDefinition(Guid.NewGuid(), "ssh", "host.example", 22, ProtocolKind.Ssh2,
                    new CredentialReference("1password", "vault/item"), ParentFolderId: folderId, SortOrder: 1,
                    Options: options, GatewayCredential: new CredentialReference("1password", "vault/gateway")),
                new ConnectionDefinition(Guid.NewGuid(), "terminal", string.Empty, 0, ProtocolKind.ExternalApplication,
                    CredentialReference.None,
                    new ExternalApplicationDefinition("terminal", "terminal.exe", "--host %HOSTNAME%", string.Empty,
                        RunElevated: false, EmbedWindow: true, WaitForExit: false), ParentFolderId: folderId, SortOrder: 2)
            ]);

            await store.SaveAsync(tree);
            ConnectionTreeDefinition restored = await store.LoadAsync();

            Assert.That(restored.Folders.Single().Id, Is.EqualTo(folderId));
            Assert.That(restored.Connections.Select(connection => connection.Id),
                Is.EquivalentTo(tree.Connections.Select(connection => connection.Id)));
            Assert.That(restored.Folders.Single().Options!.Values, Is.EqualTo(options.Values));
            Assert.That(restored.Connections.First().GatewayCredential,
                Is.EqualTo(new CredentialReference("1password", "vault/gateway")));
        }
        finally
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    [Test]
    public void RejectsBlankConnectionString()
    {
        Assert.That(() => new SqliteConnectionDefinitionStore(string.Empty), Throws.ArgumentException);
    }

    [Test]
    [TestCase(2)]
    [TestCase(4)]
    public async Task RejectsDatabaseFromUnsupportedSchemaVersion(long schemaVersion)
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"loipvremote-{Guid.NewGuid():N}.db");
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"CREATE TABLE schema_version(version INTEGER NOT NULL); INSERT INTO schema_version(version) VALUES ({schemaVersion});";
                await command.ExecuteNonQueryAsync();
            }

            var store = new SqliteConnectionDefinitionStore($"Data Source={databasePath};Pooling=False");

            Assert.That(async () => await store.LoadAsync(), Throws.InstanceOf<InvalidDataException>());
        }
        finally
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    [Test]
    public async Task RejectsDatabaseWithRemovedLegacyShape()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"loipvremote-{Guid.NewGuid():N}.db");
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE connection_definitions(id TEXT NOT NULL, name TEXT NOT NULL);";
                await command.ExecuteNonQueryAsync();
            }

            var store = new SqliteConnectionDefinitionStore($"Data Source={databasePath};Pooling=False");

            Assert.That(async () => await store.LoadAsync(), Throws.InstanceOf<InvalidDataException>());
        }
        finally
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }
}
