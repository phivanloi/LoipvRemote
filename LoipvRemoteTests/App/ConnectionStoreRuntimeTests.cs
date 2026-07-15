using LoipvRemote.App;
using LoipvRemote.Container;
using LoipvRemote.Infrastructure.Persistence;
using LoipvRemote.Infrastructure.Windows.Dpapi;
using LoipvRemote.Messages;
using LoipvRemote.Security;
using LoipvRemote.UseCases.Configuration;
using LoipvRemote.Connection;
using LoipvRemote.Config.Connections;
using LoipvRemote.Config.Putty;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;
using LoipvRemote.UI.Adapters;
using LoipvRemoteTests.TestHelpers;
using NUnit.Framework;

namespace LoipvRemoteTests.App;

public sealed class ConnectionWorkspacePersistenceTests
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
    public void XmlRuntimeRoundTripPreservesFoldersOptionsInheritanceAndCredentialReferences()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"loipvremote-runtime-{Guid.NewGuid():N}.xml");
        _temporaryFiles.Add(filePath);
        var workspace = CreateWorkspace();
        var source = new ConnectionTreeModel();
        var root = new RootNodeInfo(RootNodeType.Connection, Guid.NewGuid().ToString());
        var folder = new ContainerInfo(Guid.NewGuid().ToString()) { Name = "Production", PuttySession = "prod" };
        var connection = new ConnectionInfo(Guid.NewGuid().ToString())
        {
            Name = "ssh-prod",
            Hostname = "host.example",
            Port = 22,
            Protocol = ProtocolKind.Ssh2,
            ExternalCredentialProvider = ExternalCredentialProvider.DelineaSecretServer,
            UserViaAPI = "secret/ssh",
            Inheritance = { PuttySession = true, ExternalCredentialProvider = false, UserViaAPI = false }
        };
        root.AddChild(folder);
        folder.AddChild(connection);
        source.AddRootNode(root);

        workspace.SaveConnections(source, false, new SaveFilter(), filePath, forceSave: true);
        workspace.LoadConnections(useDatabase: false, import: true, connectionFileName: filePath);
        RootNodeInfo restoredRoot = workspace.ConnectionTreeModel.RootNodes.OfType<RootNodeInfo>().Single();
        ContainerInfo restoredFolder = restoredRoot.Children.OfType<ContainerInfo>().Single();
        ConnectionInfo restoredConnection = restoredFolder.Children.Single();

        Assert.Multiple(() =>
        {
            Assert.That(restoredFolder.PuttySession, Is.EqualTo("prod"));
            Assert.That(restoredConnection.Inheritance.PuttySession, Is.True);
            Assert.That(restoredConnection.ExternalCredentialProvider, Is.EqualTo(ExternalCredentialProvider.DelineaSecretServer));
            Assert.That(restoredConnection.UserViaAPI, Is.EqualTo("secret/ssh"));
        });
    }

    [Test]
    public void ConnectionWorkspaceLoadsXmlThroughTheDomainStoreRuntime()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"loipvremote-runtime-{Guid.NewGuid():N}.xml");
        _temporaryFiles.Add(filePath);
        var workspace = CreateWorkspace();
        var source = new ConnectionTreeModel();
        var root = new RootNodeInfo(RootNodeType.Connection, Guid.NewGuid().ToString());
        root.AddChild(new ConnectionInfo(Guid.NewGuid().ToString())
        {
            Name = "ssh",
            Hostname = "host.example",
            Port = 22,
            Protocol = ProtocolKind.Ssh2
        });
        source.AddRootNode(root);
        workspace.SaveConnections(source, false, new SaveFilter(), filePath, forceSave: true);

        workspace.LoadConnections(useDatabase: false, import: true, connectionFileName: filePath);

        Assert.That(workspace.ConnectionTreeModel.RootNodes.Single().Children.Single().Hostname, Is.EqualTo("host.example"));
    }

    [Test]
    public void LoadDoesNotDeadlockWhenCalledFromANonPumpingSynchronizationContext()
    {
        var workspace = CreateWorkspace(new YieldingStoreFactory());
        SynchronizationContext? originalContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());

        try
        {
            workspace.LoadConnections(useDatabase: false, import: true, connectionFileName: "ignored.xml");
            Assert.That(workspace.ConnectionTreeModel, Is.Not.Null);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
    }

    [Test]
    public void DomainMappingRoundTripsConnectionPasswordsThroughAProtectedOption()
    {
        var source = new ConnectionTreeModel();
        var root = new RootNodeInfo(RootNodeType.Connection, Guid.NewGuid().ToString());
        root.AddChild(new ConnectionInfo(Guid.NewGuid().ToString())
        {
            Name = "ssh",
            Hostname = "host.example",
            Port = 22,
            Protocol = ProtocolKind.Ssh2,
            Password = "connection-password"
        });
        source.AddRootNode(root);

        ConnectionTreeDefinition definition = ConnectionDefinitionMapper.ToDomainTree(
            source.RootNodes,
            (_, _, plaintext) => "protected:" + plaintext);
        ConnectionTreeModel restored = ConnectionDefinitionMapper.ToDesktopTree(
            definition,
            (_, _, protectedValue) => protectedValue["protected:".Length..]);

        Assert.That(restored.RootNodes.Single().Children.Single().Password, Is.EqualTo("connection-password"));
    }

    private static ConnectionWorkspace CreateWorkspace(IConnectionDefinitionStoreFactory? factory = null)
    {
        return new ConnectionWorkspace(
            PuttySessionsManager.Instance,
            new ConnectionDefinitionPersistenceRuntime(
                new ConnectionStoreConfigurationService(factory ?? new ConnectionDefinitionStoreFactory())),
            new XmlConnectionStoreOptionsProvider(),
            new DpapiStringSecretStore(new WindowsDpapiSecretProtector()),
            new MessageCollector());
    }

    private sealed class YieldingStoreFactory : IConnectionDefinitionStoreFactory
    {
        public IConnectionDefinitionStore Create(ConnectionDefinitionStoreOptions options) => new YieldingStore();
    }

    private sealed class YieldingStore : IConnectionDefinitionStore
    {
        public async Task<ConnectionTreeDefinition> LoadAsync(CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return new ConnectionTreeDefinition([], []);
        }

        public Task SaveAsync(ConnectionTreeDefinition tree, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
        }
    }
}
