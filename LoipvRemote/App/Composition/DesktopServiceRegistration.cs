using LoipvRemote.Config.Putty;
using LoipvRemote.App.Configuration;
using LoipvRemote.Connection;
using LoipvRemote.Connection.Protocol;
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
using LoipvRemote.Infrastructure.Windows.Registry;
using LoipvRemote.Messages;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.ExternalApps;
using LoipvRemote.Tools;
using LoipvRemote.UI.Adapters;
using LoipvRemote.UI.Panels;
using LoipvRemote.UseCases.Configuration;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.UseCases.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace LoipvRemote.App.Composition;

/// <summary>Registers the current desktop composition while source ownership is migrated into target modules.</summary>
public static class DesktopServiceRegistration
{
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<RuntimeState>();
        services.AddSingleton<MessageCollector>();
        services.AddSingleton<Startup>();
        services.AddSingleton<ExternalToolsService>();
        services.AddSingleton<ConnectionWorkspaceAdapter>();
        services.AddSingleton<PanelAdder>();
        services.AddSingleton<ICredentialRepositoryList, CredentialRepositoryList>();
        services.AddSingleton<PuttySessionsManager>(_ => PuttySessionsManager.Instance);
        services.AddSingleton<ConnectionStoreRuntime>(provider => new ConnectionStoreRuntime(
            provider.GetRequiredService<ConnectionDefinitionPersistenceRuntime>(),
            provider.GetRequiredService<IConnectionStoreOptionsProvider>(),
            provider.GetRequiredService<IStringSecretStore>(),
            connection => provider.GetRequiredService<ExternalToolsService>()
                .GetExtAppByName(connection.ExtApp)
                ?.ToDefinition(connection)));
        services.AddSingleton<ConnectionsService>(provider => new ConnectionsService(
            provider.GetRequiredService<PuttySessionsManager>(),
            provider.GetRequiredService<ConnectionStoreRuntime>(),
            provider.GetRequiredService<MessageCollector>()));
        services.AddSingleton<ConnectionLoadingService>();
        services.AddSingleton<ConnectionImportService>();
        services.AddSingleton<ConnectionExportService>();
        services.AddSingleton<DesktopShellRuntime>();

        services.AddSingleton<IUserSecretProtector, WindowsDpapiSecretProtector>();
        services.AddSingleton<DpapiStringSecretStore>();
        services.AddSingleton<IStringSecretStore>(provider => provider.GetRequiredService<DpapiStringSecretStore>());
        services.AddSingleton<IWindowsRegistryValueReader, WindowsRegistryValueReader>();
        services.AddSingleton<IExternalApplicationHostFactory, WindowsExternalApplicationHostFactory>();

        services.AddSingleton<IExternalCredentialConnector, DelineaCredentialConnector>();
        services.AddSingleton<IExternalCredentialConnector, PasswordstateCredentialConnector>();
        services.AddSingleton<IExternalCredentialConnector, OnePasswordCredentialConnector>();
        services.AddSingleton<IExternalCredentialConnector, OpenBaoCredentialConnector>();
        services.AddSingleton<ExternalCredentialConnectorRegistry>();

        services.AddSingleton<IConnectionDefinitionStoreFactory, ConnectionDefinitionStoreFactory>();
        services.AddSingleton<ConnectionStoreConfigurationService>();
        services.AddSingleton<ConnectionDefinitionPersistenceRuntime>();
        services.AddSingleton<IConnectionStoreOptionsProvider, DesktopConnectionStoreOptionsProvider>();

        services.AddSingleton<ProtocolFactory>(provider => new ProtocolFactory(
            provider.GetRequiredService<ExternalCredentialConnectorRegistry>(),
            provider.GetRequiredService<IStringSecretStore>(),
            provider.GetRequiredService<MessageCollector>(),
            provider.GetRequiredService<ConnectionWorkspaceAdapter>(),
            provider.GetRequiredService<ExternalToolsService>(),
            provider.GetRequiredService<IExternalApplicationHostFactory>()));
        services.AddSingleton<IProtocolFactory>(provider => provider.GetRequiredService<ProtocolFactory>());
        services.AddSingleton<ConnectionInitiator>(provider => new ConnectionInitiator(
            provider.GetRequiredService<ProtocolFactory>(),
            provider.GetRequiredService<ExternalToolsService>(),
            provider.GetRequiredService<RuntimeState>(),
            provider.GetRequiredService<MessageCollector>(),
            provider.GetRequiredService<ConnectionWorkspaceAdapter>(),
            provider.GetRequiredService<ConnectionsService>(),
            provider.GetRequiredService<PanelAdder>(),
            provider.GetRequiredService<ConnectionSessionOrchestrator>(),
            provider.GetRequiredService<SessionLifecycleCoordinator>(),
            connection => provider.GetRequiredService<ExternalToolsService>()
                .GetExtAppByName(connection.ExtApp)
                ?.ToDefinition(connection)));
    }
}
