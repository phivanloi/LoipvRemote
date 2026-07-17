using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Infrastructure.Persistence;
using LoipvRemote.Infrastructure.Persistence.Xml;
using LoipvRemote.Application.Configuration;
using LoipvRemote.Application.Credentials;
using LoipvRemote.WinUI.Services;
using LoipvRemote.WinUI.ViewModels;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Services;

public sealed class ConnectionCatalogTests
{
    [Test]
    public async Task LoadAsyncReturnsAnEmptyTreeWhenTheSelectedXmlFileDoesNotExist()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            ConnectionCatalog catalog = CreateCatalog(Path.Combine(directory, "settings.json"), Path.Combine(directory, "missing.xml"));

            ConnectionCatalogLoadResult result = await catalog.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.Tree.Folders, Is.Empty);
                Assert.That(result.Tree.Connections, Is.Empty);
                Assert.That(result.Message, Is.EqualTo("No connection file has been created yet."));
            });
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task SaveAsyncRoundTripsAnEditedTreeWithoutDroppingExistingNodes()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        string file = Path.Combine(directory, "confCons.xml");
        Directory.CreateDirectory(directory);
        try
        {
            Guid folderId = Guid.NewGuid();
            var existing = new ConnectionDefinition(Guid.NewGuid(), "RDP", "rdp.example", 3389, ProtocolKind.Rdp, CredentialReference.None, ParentFolderId: folderId);
            var initial = new ConnectionTreeDefinition([new ConnectionFolderDefinition(folderId, "Servers", IsRoot: true)], [existing]);
            await new XmlConnectionDefinitionStore(file).SaveAsync(initial);
            var catalog = CreateCatalog(settingsFile: Path.Combine(directory, "settings.json"), file);

            ConnectionCatalogLoadResult loaded = await catalog.LoadAsync();
            ConnectionTreeDefinition edited = ConnectionTreeEditor.AddRootConnection(loaded.Tree, "SSH", "ssh.example", 22, ProtocolKind.Ssh2);
            await catalog.SaveAsync(edited);

            ConnectionCatalogLoadResult reloaded = await CreateCatalog(settingsFile: Path.Combine(directory, "settings-reload.json"), file).LoadAsync();
            Assert.Multiple(() =>
            {
                Assert.That(reloaded.Tree.Folders, Is.EqualTo(initial.Folders));
                Assert.That(reloaded.Tree.Connections, Has.Count.EqualTo(2));
                Assert.That(reloaded.Tree.Connections.Single(connection => connection.Name == "RDP"), Is.EqualTo(existing));
                Assert.That(reloaded.Tree.Connections.Single(connection => connection.Name == "SSH").Host, Is.EqualTo("ssh.example"));
            });
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task ChangeStoreAsyncMigratesTheCurrentTreeOnlyWhenExplicitlyRequested()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        string sourceFile = Path.Combine(directory, "source.xml");
        string targetFile = Path.Combine(directory, "target.xml");
        Directory.CreateDirectory(directory);
        try
        {
            ConnectionTreeDefinition source = new([], [new ConnectionDefinition(Guid.NewGuid(), "SSH", "ssh.example", 22, ProtocolKind.Ssh2, CredentialReference.None)]);
            await new XmlConnectionDefinitionStore(sourceFile).SaveAsync(source);
            ConnectionCatalog catalog = CreateCatalog(Path.Combine(directory, "settings.json"), sourceFile);
            await catalog.LoadAsync();

            ConnectionCatalogLoadResult result = await catalog.ChangeStoreAsync(
                new ConnectionStoreSettings(ConnectionDefinitionStoreKind.Xml, targetFile),
                migrateCurrentTree: true);

            ConnectionTreeDefinition migrated = await new XmlConnectionDefinitionStore(targetFile).LoadAsync();
            Assert.Multiple(() =>
            {
                Assert.That(result.Tree.Folders, Is.Empty);
                Assert.That(result.Tree.Connections, Has.Count.EqualTo(1));
                Assert.That(result.Tree.Connections.Single(), Is.EqualTo(source.Connections.Single()));
                Assert.That(catalog.Settings.Location, Is.EqualTo(targetFile));
                Assert.That(migrated.Connections, Has.Count.EqualTo(1));
            });
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static ConnectionCatalog CreateCatalog(string settingsFile, string connectionFile)
    {
        var settings = new ConnectionStoreSettingsRepository(new ReversibleSecretStore(), settingsFile);
        var catalog = new ConnectionCatalog(
            new ConnectionStoreConfigurationService(new ConnectionDefinitionStoreFactory()),
            settings);
        settings.SaveAsync(new ConnectionStoreSettings(ConnectionDefinitionStoreKind.Xml, connectionFile)).GetAwaiter().GetResult();
        return catalog;
    }

    private sealed class ReversibleSecretStore : IStringSecretStore
    {
        public string Protect(string plaintext, string purpose) => "protected:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));

        public string Unprotect(string protectedValue, string purpose) =>
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue["protected:".Length..]));
    }
}
