using System;
using System.Linq;
using LoipvRemote.Connection;
using LoipvRemote.Connection.Protocol;
using LoipvRemote.Container;
using LoipvRemote.Domain.Connections;
using LoipvRemote.UI.Adapters;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Adapters;

public class ConnectionDefinitionMapperTests
{
    [Test]
    public void MapsLegacyConnectionWithoutLeakingPassword()
    {
        var connection = new ConnectionInfo(Guid.NewGuid().ToString())
        {
            Name = "ssh-prod",
            Hostname = "server.example",
            Port = 22,
            Protocol = ProtocolType.SSH2,
            Password = "must-not-cross-domain-boundary"
        };

        var definition = ConnectionDefinitionMapper.ToDomain(connection);

        Assert.Multiple(() =>
        {
            Assert.That(definition.Protocol, Is.EqualTo(ProtocolKind.Ssh2));
            Assert.That(definition.Host, Is.EqualTo("server.example"));
            Assert.That(definition.Credential.Identifier, Is.Empty);
        });
    }

    [Test]
    public void RejectsConnectionWithNonGuidIdentifier()
    {
        var connection = new ConnectionInfo("legacy-id");

        Assert.That(
            () => ConnectionDefinitionMapper.ToDomain(connection),
            Throws.ArgumentException);
    }

    [TestCase(ProtocolType.ARD, ProtocolKind.Ard)]
    [TestCase(ProtocolType.PowerShell, ProtocolKind.PowerShell)]
    [TestCase(ProtocolType.Terminal, ProtocolKind.Terminal)]
    [TestCase(ProtocolType.WSL, ProtocolKind.Wsl)]
    [TestCase(ProtocolType.AnyDesk, ProtocolKind.AnyDesk)]
    public void MapsEachLegacyProtocolToItsMatchingDomainProtocol(ProtocolType legacyProtocol, ProtocolKind expectedProtocol)
    {
        var connection = new ConnectionInfo(Guid.NewGuid().ToString())
        {
            Name = "connection",
            Hostname = "host.example",
            Port = 1,
            Protocol = legacyProtocol
        };

        ConnectionDefinition definition = ConnectionDefinitionMapper.ToDomain(connection);

        Assert.That(definition.Protocol, Is.EqualTo(expectedProtocol));
    }

    [Test]
    public void MapsExternalApplicationThroughExplicitResolver()
    {
        var connection = new ConnectionInfo(Guid.NewGuid().ToString())
        {
            Name = "support-tool",
            Hostname = "host.example",
            Port = 0,
            Protocol = ProtocolType.IntApp,
            ExtApp = "Support Tool"
        };
        var expected = new ExternalApplicationDefinition(
            "Support Tool",
            "C:\\Tools\\support.exe",
            "--connect {HOSTNAME}",
            "C:\\Tools",
            RunElevated: false,
            EmbedWindow: true,
            WaitForExit: false);

        ConnectionDefinition definition = ConnectionDefinitionMapper.ToDomain(
            connection,
            externalApplicationResolver: _ => expected);

        Assert.Multiple(() =>
        {
            Assert.That(definition.Protocol, Is.EqualTo(ProtocolKind.ExternalApplication));
            Assert.That(definition.ExternalApplication, Is.EqualTo(expected));
        });
    }

    [Test]
    public void MapsNestedLegacyFoldersAndPreservesSiblingOrder()
    {
        var root = new ContainerInfo(Guid.NewGuid().ToString());
        var production = new ContainerInfo(Guid.NewGuid().ToString()) { Name = "Production" };
        var ssh = new ConnectionInfo(Guid.NewGuid().ToString())
        {
            Name = "ssh-prod", Hostname = "host.example", Port = 22, Protocol = ProtocolType.SSH2
        };
        production.AddChild(ssh);
        root.AddChild(production);

        ConnectionTreeDefinition tree = ConnectionDefinitionMapper.ToDomainTree([root]);

        Assert.Multiple(() =>
        {
            Assert.That(tree.Folders, Has.Count.EqualTo(1));
            Assert.That(tree.Folders.Single().Name, Is.EqualTo("Production"));
            Assert.That(tree.Folders.Single().SortOrder, Is.EqualTo(0));
            Assert.That(tree.Connections.Single().ParentFolderId, Is.EqualTo(tree.Folders.Single().Id));
            Assert.That(tree.Connections.Single().SortOrder, Is.EqualTo(0));
        });
    }

    [Test]
    public void RoundTripsFolderExpansionState()
    {
        var root = new RootNodeInfo(RootNodeType.Connection, Guid.NewGuid().ToString())
        {
            IsExpanded = true
        };
        var expandedFolder = new ContainerInfo(Guid.NewGuid().ToString())
        {
            Name = "Expanded",
            IsExpanded = true
        };
        var collapsedFolder = new ContainerInfo(Guid.NewGuid().ToString())
        {
            Name = "Collapsed",
            IsExpanded = false
        };
        root.AddChild(expandedFolder);
        root.AddChild(collapsedFolder);

        ConnectionTreeDefinition domain = ConnectionDefinitionMapper.ToDomainTree([root]);
        ConnectionTreeModel restored = ConnectionDefinitionMapper.ToDesktopTree(domain);
        RootNodeInfo restoredRoot = restored.RootNodes.OfType<RootNodeInfo>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(restoredRoot.IsExpanded, Is.True);
            Assert.That(restoredRoot.Children.OfType<ContainerInfo>().Single(folder => folder.Name == "Expanded").IsExpanded, Is.True);
            Assert.That(restoredRoot.Children.OfType<ContainerInfo>().Single(folder => folder.Name == "Collapsed").IsExpanded, Is.False);
        });
    }

    [Test]
    public void ExcludesRuntimePuttySessionsFromPersistentDefinitions()
    {
        var root = new RootNodeInfo(RootNodeType.Connection, Guid.NewGuid().ToString());
        root.AddChild(new ConnectionInfo(Guid.NewGuid().ToString())
        {
            Name = "persisted-ssh",
            Hostname = "host.example",
            Port = 22,
            Protocol = ProtocolType.SSH2
        });

        var puttySessions = new RootPuttySessionsNodeInfo();
        puttySessions.AddChild(new PuttySessionInfo());

        ConnectionTreeDefinition tree = ConnectionDefinitionMapper.ToDomainTree([root, puttySessions]);

        Assert.Multiple(() =>
        {
            Assert.That(tree.Folders, Has.Count.EqualTo(1));
            Assert.That(tree.Connections, Has.Count.EqualTo(1));
            Assert.That(tree.Connections.Single().Name, Is.EqualTo("persisted-ssh"));
        });
    }

    [Test]
    public void RoundTripsRootFoldersProtocolOptionsInheritanceAndCredentialReferences()
    {
        var root = new RootNodeInfo(RootNodeType.Connection, Guid.NewGuid().ToString());
        var production = new ContainerInfo(Guid.NewGuid().ToString())
        {
            Name = "Production",
            Protocol = ProtocolType.SSH2,
            PuttySession = "prod-defaults"
        };
        var ssh = new ConnectionInfo(Guid.NewGuid().ToString())
        {
            Name = "ssh-prod",
            Hostname = "host.example",
            Port = 22,
            Protocol = ProtocolType.SSH2,
            ExternalCredentialProvider = ExternalCredentialProvider.DelineaSecretServer,
            UserViaAPI = "folder/prod/ssh",
            Inheritance = { PuttySession = true, ExternalCredentialProvider = false, UserViaAPI = false }
        };
        root.AddChild(production);
        production.AddChild(ssh);

        ConnectionTreeDefinition domain = ConnectionDefinitionMapper.ToDomainTree([root]);
        ConnectionTreeModel restored = ConnectionDefinitionMapper.ToDesktopTree(domain);
        RootNodeInfo restoredRoot = restored.RootNodes.OfType<RootNodeInfo>().Single();
        ContainerInfo restoredFolder = restoredRoot.Children.OfType<ContainerInfo>().Single();
        ConnectionInfo restoredConnection = restoredFolder.Children.Single();

        Assert.Multiple(() =>
        {
            Assert.That(domain.Folders.Single(folder => folder.IsRoot).Id, Is.EqualTo(Guid.Parse(root.ConstantID)));
            Assert.That(domain.Connections.Single().Credential,
                Is.EqualTo(new LoipvRemote.Domain.Credentials.CredentialReference("DelineaSecretServer", "folder/prod/ssh")));
            Assert.That(domain.Folders.Single(folder => folder.Name == "Production").Options!
                .Values[nameof(ConnectionInfo.PuttySession)], Is.EqualTo("prod-defaults"));
            Assert.That(domain.Connections.Single().Options!.InheritedProperties, Does.Contain(nameof(ConnectionInfo.PuttySession)));
            Assert.That(restoredFolder.PuttySession, Is.EqualTo("prod-defaults"));
            Assert.That(restoredConnection.Inheritance.PuttySession, Is.True);
            Assert.That(restoredConnection.ExternalCredentialProvider, Is.EqualTo(ExternalCredentialProvider.DelineaSecretServer));
            Assert.That(restoredConnection.UserViaAPI, Is.EqualTo("folder/prod/ssh"));
        });
    }
}
