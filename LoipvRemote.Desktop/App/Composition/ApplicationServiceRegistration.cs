using LoipvRemote.App.Configuration;
using LoipvRemote.Config.Putty;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.Connectors.Delinea;
using LoipvRemote.Connectors.OnePassword;
using LoipvRemote.Connectors.OpenBao;
using LoipvRemote.Connectors.Passwordstate;
using LoipvRemote.Credential;
using LoipvRemote.Credential.Repositories;
using LoipvRemote.Infrastructure.Persistence;
using LoipvRemote.Infrastructure.Windows.Dpapi;
using LoipvRemote.Infrastructure.Windows.ProcessManagement;
using LoipvRemote.Infrastructure.Windows.Process;
using LoipvRemote.Infrastructure.Windows.Registry;
using LoipvRemote.Messages;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Infrastructure.Windows.Com;
using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using LoipvRemote.Desktop.Composition;
using LoipvRemote.Tools;
using LoipvRemote.Tree;
using LoipvRemote.UI.Adapters;
using LoipvRemote.UI.Panels;
using LoipvRemote.UseCases.Configuration;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.UseCases.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace LoipvRemote.App.Composition;

/// <summary>Registers the current desktop composition while source ownership is migrated into target modules.</summary>
public static class ApplicationServiceRegistration
{
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<RuntimeState>();
        services.AddSingleton<MessageCollector>();
        services.AddSingleton<Startup>();
        services.AddSingleton<IExternalToolRuntime, ExternalToolRuntime>();
        services.AddSingleton<ExternalToolsService>();
        services.AddSingleton<ConnectionWorkspaceAdapter>();
        services.AddSingleton<PanelAdder>();
        services.AddSingleton<ICredentialRepositoryList, CredentialRepositoryList>();
        services.AddSingleton<PuttySessionsManager>(_ => PuttySessionsManager.Instance);
        services.AddSingleton<IConnectionTreeWorkspace>(provider => new ConnectionWorkspace(
            provider.GetRequiredService<PuttySessionsManager>(),
            provider.GetRequiredService<ConnectionDefinitionPersistenceRuntime>(),
            provider.GetRequiredService<IConnectionStoreOptionsProvider>(),
            provider.GetRequiredService<IStringSecretStore>(),
            provider.GetRequiredService<MessageCollector>(),
            connection => provider.GetRequiredService<ExternalToolsService>()
                .GetExtAppByName(connection.ExtApp)
                ?.ToDefinition(connection),
            withDialog => provider.GetRequiredService<ConnectionLoadingService>().LoadConnections(withDialog)));
        services.AddSingleton<IConnectionWorkspace>(provider => provider.GetRequiredService<IConnectionTreeWorkspace>());
        services.AddSingleton<ConnectionLoadingService>(provider => new ConnectionLoadingService(
            provider.GetRequiredService<IConnectionWorkspace>(),
            provider.GetRequiredService<MessageCollector>(),
            provider.GetRequiredService<ConnectionWorkspaceAdapter>(),
            provider.GetRequiredService<ConnectionImportService>(),
            () => provider.GetRequiredService<IConnectionTreeWorkspace>().ConnectionTreeModel.RootNodes[0]));
        services.AddSingleton<ConnectionImportService>();
        services.AddSingleton<ConnectionExportService>();
        services.AddSingleton<DesktopShellRuntime>();

        services.AddSingleton<IUserSecretProtector, WindowsDpapiSecretProtector>();
        services.AddSingleton<DpapiStringSecretStore>();
        services.AddSingleton<IStringSecretStore>(provider => provider.GetRequiredService<DpapiStringSecretStore>());
        services.AddSingleton<IConnectionSecretResolver, DesktopConnectionSecretResolver>();
        services.AddSingleton<IWindowsRegistryValueReader, WindowsRegistryValueReader>();
        services.AddSingleton<IExternalApplicationHostFactory, WindowsExternalApplicationHostFactory>();

        services.AddSingleton<IExternalCredentialConnector, DelineaCredentialConnector>();
        services.AddSingleton<IExternalCredentialConnector, PasswordstateCredentialConnector>();
        services.AddSingleton<IExternalCredentialConnector, OnePasswordCredentialConnector>();
        services.AddSingleton<IExternalCredentialConnector, OpenBaoCredentialConnector>();
        services.AddSingleton<ExternalCredentialConnectorRegistry>();
        services.AddSingleton<DesktopExternalCredentialResolver>();

        services.AddSingleton<IConnectionDefinitionStoreFactory, ConnectionDefinitionStoreFactory>();
        services.AddSingleton<ConnectionStoreConfigurationService>();
        services.AddSingleton<ConnectionDefinitionPersistenceRuntime>();
        services.AddSingleton<IConnectionStoreOptionsProvider, DesktopConnectionStoreOptionsProvider>();
        services.AddSingleton<IPuttyExecutablePathProvider, ConfiguredPuttyExecutablePathProvider>();

        services.AddSingleton<ConnectionInitiator>(provider => new ConnectionInitiator(
            provider.GetRequiredService<IProtocolFactory>(),
            provider.GetRequiredService<ExternalToolsService>(),
            provider.GetRequiredService<RuntimeState>(),
            provider.GetRequiredService<MessageCollector>(),
            provider.GetRequiredService<ConnectionWorkspaceAdapter>(),
            name =>
            {
                ConnectionTreeModel? tree = provider.GetRequiredService<IConnectionTreeWorkspace>().ConnectionTreeModel;
                return tree is null ? null : FindSshConnection(tree.RootNodes, name);
            },
            provider.GetRequiredService<PanelAdder>(),
            provider.GetRequiredService<ConnectionSessionOrchestrator>(),
            provider.GetRequiredService<SessionLifecycleCoordinator>(),
            connection => provider.GetRequiredService<ExternalToolsService>()
                .GetExtAppByName(connection.ExtApp)
                ?.ToDefinition(connection),
            (connectionId, propertyName, plaintext) => provider.GetRequiredService<IStringSecretStore>()
                .Protect(plaintext, ConnectionSecretPurposes.ForConnectionOption(connectionId, propertyName)),
            (connection, gateway) => provider.GetRequiredService<DesktopExternalCredentialResolver>()
                .Resolve(connection, gateway)));
    }

    private static ConnectionInfo? FindSshConnection(IEnumerable<ConnectionInfo> nodes, string name)
    {
        foreach (ConnectionInfo node in nodes)
        {
            if (node is ContainerInfo container)
            {
                ConnectionInfo? nested = FindSshConnection(container.Children, name);
                if (nested is not null)
                    return nested;
            }
            else if (node.Name == name && node.Protocol is ProtocolKind.Ssh1 or ProtocolKind.Ssh2)
            {
                return node;
            }
        }

        return null;
    }
}
