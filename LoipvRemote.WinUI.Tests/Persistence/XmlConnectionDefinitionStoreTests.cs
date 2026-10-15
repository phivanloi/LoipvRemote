using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Application.Configuration;
using LoipvRemote.Infrastructure.Persistence.Xml;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Persistence;

public sealed class XmlConnectionDefinitionStoreTests
{
    [Test]
    public async Task PortableExportRoundTripsConnectionCredentialsAsPlaintext()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directory, "connections.xml");
        ConnectionTreeDefinition tree = CreateTree("Portable", "portable.example");
        ConnectionDefinition connection = tree.Connections.Single();
        var store = new XmlConnectionDefinitionStore(filePath);
        try
        {
            await store.SavePortableAsync(
                tree,
                new Dictionary<Guid, PortableConnectionCredential>
                {
                    [connection.Id] = new("administrator", "portable-password", "gateway-password")
                });

            ConnectionExportPackage imported = await store.LoadPortableAsync();

            string xml = await File.ReadAllTextAsync(filePath);
            Assert.Multiple(() =>
            {
                Assert.That(imported.Tree.Connections.Single(), Is.EqualTo(connection));
                Assert.That(imported.Credentials[connection.Id], Is.EqualTo(new PortableConnectionCredential("administrator", "portable-password", "gateway-password")));
                Assert.That(xml, Does.Contain("portable-password"));
            });
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task SaveAsyncKeepsThePreviousXmlTreeAsAVersionedBackup()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directory, "connections.xml");
        var store = new XmlConnectionDefinitionStore(filePath);
        ConnectionTreeDefinition original = CreateTree("Before", "before.example");
        ConnectionTreeDefinition replacement = CreateTree("After", "after.example");
        try
        {
            await store.SaveAsync(original);
            await store.SaveAsync(replacement);

            FileInfo backup = new DirectoryInfo(Path.Combine(directory, "backups"))
                .EnumerateFiles("connections.*.xml")
                .Single();
            ConnectionTreeDefinition backupTree = await new XmlConnectionDefinitionStore(backup.FullName).LoadAsync();
            ConnectionTreeDefinition currentTree = await store.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(backupTree.Connections.Single().Host, Is.EqualTo("before.example"));
                Assert.That(currentTree.Connections.Single().Host, Is.EqualTo("after.example"));
            });
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static ConnectionTreeDefinition CreateTree(string name, string host) => new(
        [],
        [new ConnectionDefinition(Guid.NewGuid(), name, host, 22, ProtocolKind.Ssh2, CredentialReference.None)]);
}
