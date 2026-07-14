using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Infrastructure.Persistence.Xml;
using NUnit.Framework;

namespace LoipvRemoteTests.Infrastructure.Persistence;

[TestFixture]
[Apartment(ApartmentState.MTA)]
public class XmlConnectionDefinitionStoreTests
{
    private readonly List<string> _temporaryFiles = [];

    [TearDown]
    public void TearDown()
    {
        foreach (string filePath in _temporaryFiles)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Test]
    public async Task RoundTripsSecretFreeDomainDefinitions()
    {
        var definition = new ConnectionDefinition(
            Guid.NewGuid(),
            "production ssh",
            "host.example",
            22,
            ProtocolKind.Ssh2,
            new CredentialReference("1password", "vault/item"));
        string filePath = CreateTemporaryFilePath();
        var store = new XmlConnectionDefinitionStore(filePath);

        var tree = new ConnectionTreeDefinition([], [definition]);
        await store.SaveAsync(tree);
        string serialized = await File.ReadAllTextAsync(filePath);
        var restored = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(restored.Folders, Is.EqualTo(tree.Folders));
            Assert.That(restored.Connections, Is.EqualTo(tree.Connections));
            Assert.That(serialized, Does.Not.Contain("<password"));
            Assert.That(serialized, Does.Not.Contain("secret"));
        });
    }

    [Test]
    public async Task RejectsMalformedConnectionId()
    {
        string filePath = CreateTemporaryFilePath();
        await File.WriteAllTextAsync(filePath, "<connections><connection id=\"not-a-guid\" name=\"ssh\" host=\"host\" port=\"22\" protocol=\"Ssh2\" /></connections>");
        var store = new XmlConnectionDefinitionStore(filePath);

        Assert.That(
            async () => await store.LoadAsync(),
            Throws.InstanceOf<InvalidDataException>());
    }

    [Test]
    public async Task RoundTripsExternalApplicationDefinition()
    {
        var externalApplication = new ExternalApplicationDefinition(
            "terminal",
            "terminal.exe",
            "--host %HOSTNAME%",
            "C:\\Tools",
            RunElevated: false,
            EmbedWindow: true,
            WaitForExit: false);
        var definition = new ConnectionDefinition(
            Guid.NewGuid(),
            "terminal",
            string.Empty,
            0,
            ProtocolKind.ExternalApplication,
            CredentialReference.None,
            externalApplication);
        string filePath = CreateTemporaryFilePath();
        var store = new XmlConnectionDefinitionStore(filePath);

        var tree = new ConnectionTreeDefinition([], [definition]);
        await store.SaveAsync(tree);
        string serialized = await File.ReadAllTextAsync(filePath);
        ConnectionTreeDefinition restored = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(restored.Folders, Is.EqualTo(tree.Folders));
            Assert.That(restored.Connections, Is.EqualTo(tree.Connections));
            Assert.That(serialized, Does.Contain("externalExecutablePath=\"terminal.exe\""));
            Assert.That(serialized, Does.Not.Contain("<password"));
        });
    }

    [Test]
    public async Task RoundTripsOptionsInheritanceAndGatewayCredentialReferences()
    {
        Guid rootId = Guid.NewGuid();
        var options = new ConnectionNodeOptions(
            new Dictionary<string, string> { ["PuttySession"] = "production" },
            ["PuttySession"]);
        var tree = new ConnectionTreeDefinition(
        [
            new ConnectionFolderDefinition(rootId, "Connections", SortOrder: 0, Options: options, IsRoot: true)
        ],
        [
            new ConnectionDefinition(Guid.NewGuid(), "ssh", "host.example", 22, ProtocolKind.Ssh2,
                new CredentialReference("DelineaSecretServer", "server/ssh"), ParentFolderId: rootId,
                Options: options,
                GatewayCredential: new CredentialReference("OnePassword", "vault/gateway"))
        ]);
        string filePath = CreateTemporaryFilePath();
        var store = new XmlConnectionDefinitionStore(filePath);

        await store.SaveAsync(tree);
        ConnectionTreeDefinition restored = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(restored.Folders.Single().IsRoot, Is.True);
            Assert.That(restored.Folders.Single().Options!.Values, Is.EqualTo(options.Values));
            Assert.That(restored.Folders.Single().Options!.InheritedProperties,
                Is.EqualTo(options.InheritedProperties));
            Assert.That(restored.Connections.Single().Options!.Values, Is.EqualTo(options.Values));
            Assert.That(restored.Connections.Single().GatewayCredential,
                Is.EqualTo(new CredentialReference("OnePassword", "vault/gateway")));
        });
    }

    private string CreateTemporaryFilePath()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"loipvremote-{Guid.NewGuid():N}.xml");
        _temporaryFiles.Add(filePath);
        return filePath;
    }
}
