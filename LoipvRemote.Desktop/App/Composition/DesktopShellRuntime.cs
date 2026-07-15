using LoipvRemote.Connection;
using LoipvRemote.Credential;
using LoipvRemote.Messages;
using LoipvRemote.Tools;
using LoipvRemote.App;
using LoipvRemote.UI.Adapters;
using LoipvRemote.UI.Panels;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.UseCases.Configuration;
using LoipvRemote.Infrastructure.Windows.WindowActivation;

namespace LoipvRemote.App.Composition;

/// <summary>Host-owned dependencies required by the WinForms shell after construction.</summary>
public sealed class DesktopShellRuntime(
    MessageCollector messageCollector,
    RuntimeState runtimeState,
    IConnectionTreeWorkspace connectionTreeWorkspace,
    ConnectionLoadingService connectionLoadingService,
    ConnectionInitiator connectionInitiator,
    ExternalToolsService externalToolsService,
    IExternalToolRuntime externalToolRuntime,
    ConnectionImportService connectionImportService,
    ConnectionExportService connectionExportService,
    ConnectionWorkspaceAdapter connectionWorkspace,
    PanelAdder panelAdder,
    ICredentialRepositoryList credentialRepositoryList,
    IStringSecretStore userSecretStore,
    IWindowActivationService windowActivationService,
    Startup startup)
{
    public MessageCollector MessageCollector { get; } = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
    public RuntimeState RuntimeState { get; } = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
    public IConnectionTreeWorkspace ConnectionTreeWorkspace { get; } = connectionTreeWorkspace ?? throw new ArgumentNullException(nameof(connectionTreeWorkspace));
    public IConnectionWorkspace ConnectionWorkspaceRuntime => ConnectionTreeWorkspace;
    public ConnectionLoadingService ConnectionLoadingService { get; } = connectionLoadingService ?? throw new ArgumentNullException(nameof(connectionLoadingService));
    public ConnectionInitiator ConnectionInitiator { get; } = connectionInitiator ?? throw new ArgumentNullException(nameof(connectionInitiator));
    public ExternalToolsService ExternalToolsService { get; } = externalToolsService ?? throw new ArgumentNullException(nameof(externalToolsService));
    public IExternalToolRuntime ExternalToolRuntime { get; } = externalToolRuntime ?? throw new ArgumentNullException(nameof(externalToolRuntime));
    public ConnectionImportService ConnectionImportService { get; } = connectionImportService ?? throw new ArgumentNullException(nameof(connectionImportService));
    public ConnectionExportService ConnectionExportService { get; } = connectionExportService ?? throw new ArgumentNullException(nameof(connectionExportService));
    public ConnectionWorkspaceAdapter ConnectionWorkspace { get; } = connectionWorkspace ?? throw new ArgumentNullException(nameof(connectionWorkspace));
    public PanelAdder PanelAdder { get; } = panelAdder ?? throw new ArgumentNullException(nameof(panelAdder));
    public ICredentialRepositoryList CredentialRepositoryList { get; } = credentialRepositoryList ?? throw new ArgumentNullException(nameof(credentialRepositoryList));
    public IStringSecretStore UserSecretStore { get; } = userSecretStore ?? throw new ArgumentNullException(nameof(userSecretStore));
    public IWindowActivationService WindowActivationService { get; } = windowActivationService ?? throw new ArgumentNullException(nameof(windowActivationService));
    public Startup Startup { get; } = startup ?? throw new ArgumentNullException(nameof(startup));
}
