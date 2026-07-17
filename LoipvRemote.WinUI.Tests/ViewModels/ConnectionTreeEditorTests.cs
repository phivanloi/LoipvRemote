using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.WinUI.ViewModels;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.ViewModels;

public sealed class ConnectionTreeEditorTests
{
    [Test]
    public void MergeImportedTreeCreatesAnIsolationFolderAndRemapsEveryImportedIdentifier()
    {
        Guid existingFolderId = Guid.NewGuid();
        Guid importedFolderId = Guid.NewGuid();
        Guid importedConnectionId = Guid.NewGuid();
        var destination = new ConnectionTreeDefinition(
            [new ConnectionFolderDefinition(existingFolderId, "Existing")],
            [new ConnectionDefinition(Guid.NewGuid(), "Live", "live.example", 22, ProtocolKind.Ssh2, Domain.Credentials.CredentialReference.None, ParentFolderId: existingFolderId)]);
        var imported = new ConnectionTreeDefinition(
            [new ConnectionFolderDefinition(importedFolderId, "Imported servers", IsRoot: true)],
            [new ConnectionDefinition(importedConnectionId, "Imported", "imported.example", 3389, ProtocolKind.Rdp, Domain.Credentials.CredentialReference.None, ParentFolderId: importedFolderId)]);

        ConnectionTreeDefinition result = ConnectionTreeEditor.MergeImportedTree(destination, imported, "Import batch");
        ConnectionFolderDefinition importRoot = result.Folders.Single(folder => folder.Name == "Import batch");
        ConnectionFolderDefinition remappedFolder = result.Folders.Single(folder => folder.Name == "Imported servers");
        ConnectionDefinition remappedConnection = result.Connections.Single(connection => connection.Name == "Imported");

        Assert.Multiple(() =>
        {
            Assert.That(result.Folders, Has.Count.EqualTo(3));
            Assert.That(result.Connections, Has.Count.EqualTo(2));
            Assert.That(remappedFolder.Id, Is.Not.EqualTo(importedFolderId));
            Assert.That(remappedFolder.ParentFolderId, Is.EqualTo(importRoot.Id));
            Assert.That(remappedConnection.Id, Is.Not.EqualTo(importedConnectionId));
            Assert.That(remappedConnection.ParentFolderId, Is.EqualTo(remappedFolder.Id));
            Assert.That(result.Connections.Single(connection => connection.Name == "Live").ParentFolderId, Is.EqualTo(existingFolderId));
        });
    }

    [Test]
    public void MergeImportedTreePreservesNestedFoldersAndRemapsTheirParentChain()
    {
        Guid importedRootId = Guid.NewGuid();
        Guid importedChildId = Guid.NewGuid();
        Guid importedConnectionId = Guid.NewGuid();
        ConnectionTreeDefinition imported = new(
            [
                new ConnectionFolderDefinition(importedRootId, "Imported root", IsRoot: true),
                new ConnectionFolderDefinition(importedChildId, "Nested", importedRootId)
            ],
            [new ConnectionDefinition(importedConnectionId, "Nested SSH", "nested.example", 22, ProtocolKind.Ssh2, CredentialReference.None, ParentFolderId: importedChildId)]);

        ConnectionTreeDefinition result = ConnectionTreeEditor.MergeImportedTree(
            ConnectionTreeDefinition.Empty,
            imported,
            "Imported batch");

        ConnectionFolderDefinition batch = result.Folders.Single(folder => folder.Name == "Imported batch");
        ConnectionFolderDefinition remappedRoot = result.Folders.Single(folder => folder.Name == "Imported root");
        ConnectionFolderDefinition remappedChild = result.Folders.Single(folder => folder.Name == "Nested");
        ConnectionDefinition remappedConnection = result.Connections.Single();

        Assert.Multiple(() =>
        {
            Assert.That(remappedRoot.Id, Is.Not.EqualTo(importedRootId));
            Assert.That(remappedRoot.ParentFolderId, Is.EqualTo(batch.Id));
            Assert.That(remappedChild.Id, Is.Not.EqualTo(importedChildId));
            Assert.That(remappedChild.ParentFolderId, Is.EqualTo(remappedRoot.Id));
            Assert.That(remappedConnection.Id, Is.Not.EqualTo(importedConnectionId));
            Assert.That(remappedConnection.ParentFolderId, Is.EqualTo(remappedChild.Id));
        });
    }

    [Test]
    public void AddRootFolderPreservesConnectionsAndAddsAtTheEnd()
    {
        var existingFolder = new ConnectionFolderDefinition(Guid.NewGuid(), "Existing", SortOrder: 3);
        var existingConnection = new ConnectionDefinition(Guid.NewGuid(), "SSH", "ssh.example", 22, ProtocolKind.Ssh2, Domain.Credentials.CredentialReference.None);
        var source = new ConnectionTreeDefinition([existingFolder], [existingConnection]);

        ConnectionTreeDefinition result = ConnectionTreeEditor.AddRootFolder(source, "New folder");

        Assert.Multiple(() =>
        {
            Assert.That(result.Connections, Is.EqualTo(source.Connections));
            Assert.That(result.Folders, Has.Count.EqualTo(2));
            Assert.That(result.Folders.Last().Name, Is.EqualTo("New folder"));
            Assert.That(result.Folders.Last().SortOrder, Is.EqualTo(4));
        });
    }

    [Test]
    public void AddRootConnectionPreservesFoldersExistingConnectionsAndAddsAtTheEnd()
    {
        Guid folderId = Guid.NewGuid();
        var existing = new ConnectionDefinition(Guid.NewGuid(), "Existing", "existing.example", 3389, ProtocolKind.Rdp, Domain.Credentials.CredentialReference.None, SortOrder: 5);
        var source = new ConnectionTreeDefinition([new ConnectionFolderDefinition(folderId, "Servers")], [existing]);

        ConnectionTreeDefinition result = ConnectionTreeEditor.AddRootConnection(source, "SSH", "ssh.example", 22, ProtocolKind.Ssh2);

        Assert.Multiple(() =>
        {
            Assert.That(result.Folders, Is.EqualTo(source.Folders));
            Assert.That(result.Connections, Has.Count.EqualTo(2));
            Assert.That(result.Connections.Last().Name, Is.EqualTo("SSH"));
            Assert.That(result.Connections.Last().Host, Is.EqualTo("ssh.example"));
            Assert.That(result.Connections.Last().SortOrder, Is.EqualTo(6));
        });
    }

    [Test]
    public void AddRootConnectionRejectsAnInvalidPortBeforePersisting()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ConnectionTreeEditor.AddRootConnection(ConnectionTreeDefinition.Empty, "SSH", "ssh.example", 0, ProtocolKind.Ssh2));
    }

    [Test]
    public void UpdateConnectionPreservesProtocolCredentialsOptionsAndTreePlacement()
    {
        Guid folderId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        var credentials = new Domain.Credentials.CredentialReference("vault", "production-ssh");
        var options = new ConnectionNodeOptions(new Dictionary<string, string> { ["Username"] = "admin" }, []);
        var original = new ConnectionDefinition(connectionId, "Old", "old.example", 22, ProtocolKind.Ssh2, credentials, ParentFolderId: folderId, SortOrder: 7, Options: options);
        var source = new ConnectionTreeDefinition([new ConnectionFolderDefinition(folderId, "Servers")], [original]);

        ConnectionTreeDefinition result = ConnectionTreeEditor.UpdateConnection(source, connectionId, "New", "new.example", 2222);
        ConnectionDefinition edited = result.Connections.Single();

        Assert.Multiple(() =>
        {
            Assert.That(edited.Name, Is.EqualTo("New"));
            Assert.That(edited.Host, Is.EqualTo("new.example"));
            Assert.That(edited.Port, Is.EqualTo(2222));
            Assert.That(edited.Protocol, Is.EqualTo(ProtocolKind.Ssh2));
            Assert.That(edited.Credential, Is.EqualTo(credentials));
            Assert.That(edited.Options, Is.EqualTo(options));
            Assert.That(edited.ParentFolderId, Is.EqualTo(folderId));
            Assert.That(edited.SortOrder, Is.EqualTo(7));
        });
    }

    [Test]
    public void UpdateFolderPreservesHierarchyAndReplacesItsInheritedOptions()
    {
        Guid rootId = Guid.NewGuid();
        Guid childId = Guid.NewGuid();
        var originalOptions = new ConnectionNodeOptions(new Dictionary<string, string> { ["Username"] = "old" }, []);
        var replacementOptions = new ConnectionNodeOptions(new Dictionary<string, string> { ["Username"] = "operator", ["SmartSize"] = "true" }, []);
        ConnectionTreeDefinition source = new(
            [
                new ConnectionFolderDefinition(rootId, "Root", Options: originalOptions, IsRoot: true),
                new ConnectionFolderDefinition(childId, "Child", rootId, SortOrder: 4)
            ],
            []);

        ConnectionTreeDefinition result = ConnectionTreeEditor.UpdateFolder(source, rootId, "Production", replacementOptions);
        ConnectionFolderDefinition root = result.Folders.Single(folder => folder.Id == rootId);
        ConnectionFolderDefinition child = result.Folders.Single(folder => folder.Id == childId);

        Assert.Multiple(() =>
        {
            Assert.That(root.Name, Is.EqualTo("Production"));
            Assert.That(root.Options, Is.EqualTo(replacementOptions));
            Assert.That(root.IsRoot, Is.True);
            Assert.That(child.ParentFolderId, Is.EqualTo(rootId));
            Assert.That(child.SortOrder, Is.EqualTo(4));
        });
    }

    [Test]
    public void AddMoveDuplicateAndDeleteConnectionPreservesAValidNestedTree()
    {
        Guid parentId = Guid.NewGuid();
        Guid childId = Guid.NewGuid();
        ConnectionTreeDefinition source = new(
            [
                new ConnectionFolderDefinition(parentId, "Parent"),
                new ConnectionFolderDefinition(childId, "Child", parentId)
            ],
            []);

        ConnectionTreeDefinition withConnection = ConnectionTreeEditor.AddConnection(
            source, "SSH", "ssh.example", 22, ProtocolKind.Ssh2, childId);
        Guid connectionId = withConnection.Connections.Single().Id;
        ConnectionTreeDefinition moved = ConnectionTreeEditor.MoveConnection(withConnection, connectionId, parentId);
        ConnectionTreeDefinition duplicated = ConnectionTreeEditor.DuplicateConnection(moved, connectionId);
        ConnectionTreeDefinition deleted = ConnectionTreeEditor.DeleteFolder(duplicated, parentId);

        Assert.Multiple(() =>
        {
            Assert.That(moved.Connections.Single().ParentFolderId, Is.EqualTo(parentId));
            Assert.That(duplicated.Connections, Has.Count.EqualTo(2));
            Assert.That(duplicated.Connections.Last().Name, Is.EqualTo("SSH (Copy)"));
            Assert.That(deleted.Folders, Is.Empty);
            Assert.That(deleted.Connections, Is.Empty);
        });
    }

    [Test]
    public void MoveFolderRejectsItsOwnDescendant()
    {
        Guid parentId = Guid.NewGuid();
        Guid childId = Guid.NewGuid();
        ConnectionTreeDefinition source = new(
            [
                new ConnectionFolderDefinition(parentId, "Parent"),
                new ConnectionFolderDefinition(childId, "Child", parentId)
            ],
            []);

        Assert.That(
            () => ConnectionTreeEditor.MoveFolder(source, parentId, childId),
            Throws.ArgumentException);
    }

    [Test]
    public void ReorderConnectionUpdatesOnlyTheSelectedSiblingOrder()
    {
        Guid folderId = Guid.NewGuid();
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        ConnectionTreeDefinition source = new(
            [new ConnectionFolderDefinition(folderId, "Servers")],
            [
                new ConnectionDefinition(firstId, "First", "first.example", 22, ProtocolKind.Ssh2, Domain.Credentials.CredentialReference.None, ParentFolderId: folderId, SortOrder: 0),
                new ConnectionDefinition(secondId, "Second", "second.example", 22, ProtocolKind.Ssh2, Domain.Credentials.CredentialReference.None, ParentFolderId: folderId, SortOrder: 1)
            ]);

        ConnectionTreeDefinition result = ConnectionTreeEditor.ReorderConnection(source, secondId, -1);

        Assert.That(result.Connections.OrderBy(connection => connection.SortOrder).Select(connection => connection.Id), Is.EqualTo([secondId, firstId]));
    }

    [Test]
    public void DuplicateFolderCopiesTheCompleteSubtreeWithoutSharingIds()
    {
        Guid parentId = Guid.NewGuid();
        Guid childId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        var options = new ConnectionNodeOptions(new Dictionary<string, string> { ["Username"] = "operator" }, []);
        ConnectionTreeDefinition source = new(
            [
                new ConnectionFolderDefinition(parentId, "Production", Options: options),
                new ConnectionFolderDefinition(childId, "Linux", parentId, SortOrder: 0)
            ],
            [new ConnectionDefinition(connectionId, "Web", "web.example", 22, ProtocolKind.Ssh2, CredentialReference.None, ParentFolderId: childId)]);

        ConnectionTreeDefinition result = ConnectionTreeEditor.DuplicateFolder(source, parentId);

        ConnectionFolderDefinition copiedParent = result.Folders.Single(folder => folder.Name == "Production (Copy)");
        ConnectionFolderDefinition copiedChild = result.Folders.Single(folder => folder.Name == "Linux" && folder.Id != childId);
        ConnectionDefinition copiedConnection = result.Connections.Single(connection => connection.Id != connectionId);
        Assert.Multiple(() =>
        {
            Assert.That(copiedParent.Id, Is.Not.EqualTo(parentId));
            Assert.That(copiedParent.Options, Is.EqualTo(options));
            Assert.That(copiedChild.ParentFolderId, Is.EqualTo(copiedParent.Id));
            Assert.That(copiedConnection.ParentFolderId, Is.EqualTo(copiedChild.Id));
            Assert.That(copiedConnection.Name, Is.EqualTo("Web"));
        });
    }

    [Test]
    public void ReorderFolderUpdatesOnlyTheSelectedSiblingOrder()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        Guid childId = Guid.NewGuid();
        ConnectionTreeDefinition source = new(
            [
                new ConnectionFolderDefinition(firstId, "First", SortOrder: 0),
                new ConnectionFolderDefinition(secondId, "Second", SortOrder: 1),
                new ConnectionFolderDefinition(childId, "Child", firstId, SortOrder: 4)
            ],
            []);

        ConnectionTreeDefinition result = ConnectionTreeEditor.ReorderFolder(source, secondId, -1);

        Assert.Multiple(() =>
        {
            Assert.That(result.Folders.Where(folder => folder.ParentFolderId is null).OrderBy(folder => folder.SortOrder).Select(folder => folder.Id), Is.EqualTo([secondId, firstId]));
            Assert.That(result.Folders.Single(folder => folder.Id == childId).SortOrder, Is.EqualTo(4));
        });
    }
}
