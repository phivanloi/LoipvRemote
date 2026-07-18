using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.WinUI.Hosting;
using LoipvRemote.WinUI.ViewModels;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.ViewModels;

public sealed class ConnectionTreeProjectionTests
{
    [Test]
    public void CreateBuildsANestedTreeAndOrdersNodesBySortOrder()
    {
        Guid rootId = Guid.NewGuid();
        Guid nestedId = Guid.NewGuid();
        Guid firstConnectionId = Guid.NewGuid();
        Guid secondConnectionId = Guid.NewGuid();
        ConnectionTreeDefinition tree = new(
            [
                new ConnectionFolderDefinition(nestedId, "Nested", rootId, SortOrder: 2),
                new ConnectionFolderDefinition(rootId, "Servers", SortOrder: 1)
            ],
            [
                new ConnectionDefinition(secondConnectionId, "Second", "second.example", 22, ProtocolKind.Ssh2, CredentialReference.None, ParentFolderId: rootId, SortOrder: 2),
                new ConnectionDefinition(firstConnectionId, "First", "first.example", 3389, ProtocolKind.Rdp, CredentialReference.None, ParentFolderId: rootId, SortOrder: 1)
            ]);

        IReadOnlyList<ConnectionTreeItem> result = ConnectionTreeProjection.Create(tree);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].DisplayName, Is.EqualTo("Servers"));
        Assert.That(result[0].Children.Select(node => node.DisplayName), Is.EqualTo([
            "Nested",
            "First: first.example",
            "Second: second.example"
        ]));
    }

    [Test]
    public void CreateKeepsRootConnectionsWhenTheTreeHasNoFolders()
    {
        ConnectionTreeDefinition tree = new(
            [],
            [new ConnectionDefinition(Guid.NewGuid(), "Gateway", "gateway.example", 3389, ProtocolKind.Rdp, CredentialReference.None)]);

        IReadOnlyList<ConnectionTreeItem> result = ConnectionTreeProjection.Create(tree);

        Assert.That(result.Select(node => node.DisplayName), Is.EqualTo(["Gateway: gateway.example"]));
    }

    [TestCase("10.11.12.135 - infras", "10.11.12.135", "infras: 10.11.12.135")]
    [TestCase("Mega: 157.66.80.18", "157.66.80.18", "Mega: 157.66.80.18")]
    [TestCase("server.example", "server.example", "server.example")]
    public void CreateConnectionDisplayNameRemovesHostRepeatedInConnectionName(string name, string host, string expected)
    {
        ConnectionDefinition connection = new(Guid.NewGuid(), name, host, 22, ProtocolKind.Ssh2, CredentialReference.None);

        Assert.That(ConnectionTreeProjection.CreateConnectionDisplayName(connection), Is.EqualTo(expected));
    }

    [Test]
    public void CreateFlattensTheLegacyRootFolder()
    {
        Guid legacyRootId = Guid.NewGuid();
        Guid childFolderId = Guid.NewGuid();
        ConnectionTreeDefinition tree = new(
            [
                new ConnectionFolderDefinition(legacyRootId, "Connections", IsRoot: true),
                new ConnectionFolderDefinition(childFolderId, "Servers", legacyRootId)
            ],
            [new ConnectionDefinition(Guid.NewGuid(), "Gateway", "gateway.example", 22, ProtocolKind.Ssh2, CredentialReference.None, ParentFolderId: legacyRootId)]);

        IReadOnlyList<ConnectionTreeItem> result = ConnectionTreeProjection.Create(tree);

        Assert.That(result.Select(node => node.DisplayName), Is.EqualTo(["Servers", "Gateway: gateway.example"]));
    }

    [Test]
    public void CreateMarksOnlyTheRequestedConnectionAsConnected()
    {
        Guid connectedId = Guid.NewGuid();
        Guid idleId = Guid.NewGuid();
        ConnectionTreeDefinition tree = new(
            [],
            [
                new ConnectionDefinition(connectedId, "Connected", "connected.example", 22, ProtocolKind.Ssh2, CredentialReference.None),
                new ConnectionDefinition(idleId, "Idle", "idle.example", 22, ProtocolKind.Ssh2, CredentialReference.None)
            ]);

        IReadOnlyList<ConnectionTreeItem> result = ConnectionTreeProjection.Create(tree, new HashSet<Guid> { connectedId });

        Assert.Multiple(() =>
        {
            Assert.That(result.Single(item => item.Id == connectedId).IsConnected, Is.True);
            Assert.That(result.Single(item => item.Id == idleId).IsConnected, Is.False);
            Assert.That(result.Single(item => item.Id == connectedId).Protocol, Is.EqualTo(ProtocolKind.Ssh2));
        });
    }

    [TestCase(ProtocolKind.Rdp, ConnectionTreeIconKind.RemoteDesktop)]
    [TestCase(ProtocolKind.Ssh2, ConnectionTreeIconKind.SshTerminal)]
    [TestCase(ProtocolKind.Vnc, ConnectionTreeIconKind.VncDesktop)]
    public void ConnectionTreeItemUsesADistinctIconForEachProtocol(ProtocolKind protocol, ConnectionTreeIconKind expectedIcon)
    {
        ConnectionTreeItem item = new(Guid.NewGuid(), "Server", false, [], Protocol: protocol);

        Assert.Multiple(() =>
        {
            Assert.That(item.IconKind, Is.EqualTo(expectedIcon));
            Assert.That(item.IconSize, Is.EqualTo(16));
            Assert.That(item.IconPathData, Is.Not.Empty);
        });
    }

    [Test]
    public void FolderTreeItemUsesACompactFolderIcon()
    {
        ConnectionTreeItem item = new(Guid.NewGuid(), "Folder", true, []);

        Assert.Multiple(() =>
        {
            Assert.That(item.IconKind, Is.EqualTo(ConnectionTreeIconKind.Folder));
            Assert.That(item.IconSize, Is.EqualTo(14));
            Assert.That(item.IconPathData, Is.Not.Empty);
        });
    }
}

public sealed class EmbeddedSessionSurfaceLayoutTests
{
    [Test]
    public void ToProtocolBoundsRemovesTheTopLevelOffset()
    {
        EmbeddedWindowBounds result = EmbeddedSessionSurfaceLayout.ToProtocolBounds(new EmbeddedWindowBounds(340, 96, 1200, 700));

        Assert.That(result, Is.EqualTo(new EmbeddedWindowBounds(0, 0, 1200, 700)));
    }
}
