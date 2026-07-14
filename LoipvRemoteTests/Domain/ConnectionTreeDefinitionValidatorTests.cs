using System;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using NUnit.Framework;

namespace LoipvRemoteTests.Domain;

public class ConnectionTreeDefinitionValidatorTests
{
    [Test]
    public void AcceptsOrderedNestedFoldersAndConnections()
    {
        Guid production = Guid.NewGuid();
        Guid ssh = Guid.NewGuid();
        var tree = new ConnectionTreeDefinition(
            [new ConnectionFolderDefinition(production, "Production")],
            [new ConnectionDefinition(ssh, "ssh", "host.example", 22, ProtocolKind.Ssh2,
                CredentialReference.None, ParentFolderId: production, SortOrder: 1)]);

        Assert.DoesNotThrow(tree.Validate);
    }

    [Test]
    public void RejectsConnectionWhoseParentFolderDoesNotExist()
    {
        var tree = new ConnectionTreeDefinition(
            [],
            [new ConnectionDefinition(Guid.NewGuid(), "ssh", "host.example", 22, ProtocolKind.Ssh2,
                CredentialReference.None, ParentFolderId: Guid.NewGuid())]);

        Assert.That(tree.Validate, Throws.ArgumentException);
    }

    [Test]
    public void RejectsFolderCycles()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        var tree = new ConnectionTreeDefinition(
            [new ConnectionFolderDefinition(first, "First", second), new ConnectionFolderDefinition(second, "Second", first)],
            []);

        Assert.That(tree.Validate, Throws.ArgumentException);
    }
}
