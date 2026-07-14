using LoipvRemote.Connection;
using LoipvRemote.Credential;
using LoipvRemote.Messages;
using LoipvRemote.Tools;
using LoipvRemote.App;
using LoipvRemote.UI.Adapters;
using LoipvRemote.UI.Panels;
using LoipvRemote.UseCases.Credentials;

namespace LoipvRemote.App.Composition;

/// <summary>Host-owned dependencies required by the WinForms shell after construction.</summary>
public sealed class DesktopShellRuntime(
    MessageCollector messageCollector,
    RuntimeState runtimeState,
    ConnectionsService connectionsService,
    ConnectionLoadingService connectionLoadingService,
    ConnectionInitiator connectionInitiator,
    ExternalToolsService externalToolsService,
    ConnectionImportService connectionImportService,
    ConnectionExportService connectionExportService,
    ConnectionWorkspaceAdapter connectionWorkspace,
    PanelAdder panelAdder,
    ICredentialRepositoryList credentialRepositoryList,
    IStringSecretStore userSecretStore,
    Startup startup)
{
    public MessageCollector MessageCollector { get; } = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
    public RuntimeState RuntimeState { get; } = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
    public ConnectionsService ConnectionsService { get; } = connectionsService ?? throw new ArgumentNullException(nameof(connectionsService));
    public ConnectionLoadingService ConnectionLoadingService { get; } = connectionLoadingService ?? throw new ArgumentNullException(nameof(connectionLoadingService));
    public ConnectionInitiator ConnectionInitiator { get; } = connectionInitiator ?? throw new ArgumentNullException(nameof(connectionInitiator));
    public ExternalToolsService ExternalToolsService { get; } = externalToolsService ?? throw new ArgumentNullException(nameof(externalToolsService));
    public ConnectionImportService ConnectionImportService { get; } = connectionImportService ?? throw new ArgumentNullException(nameof(connectionImportService));
    public ConnectionExportService ConnectionExportService { get; } = connectionExportService ?? throw new ArgumentNullException(nameof(connectionExportService));
    public ConnectionWorkspaceAdapter ConnectionWorkspace { get; } = connectionWorkspace ?? throw new ArgumentNullException(nameof(connectionWorkspace));
    public PanelAdder PanelAdder { get; } = panelAdder ?? throw new ArgumentNullException(nameof(panelAdder));
    public ICredentialRepositoryList CredentialRepositoryList { get; } = credentialRepositoryList ?? throw new ArgumentNullException(nameof(credentialRepositoryList));
    public IStringSecretStore UserSecretStore { get; } = userSecretStore ?? throw new ArgumentNullException(nameof(userSecretStore));
    public Startup Startup { get; } = startup ?? throw new ArgumentNullException(nameof(startup));
}
