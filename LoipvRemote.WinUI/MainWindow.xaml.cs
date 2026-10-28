using System.Security.Cryptography;
using LoipvRemote.Application.Configuration;
using LoipvRemote.Application.Credentials;
using LoipvRemote.Application.Sessions;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Infrastructure.Persistence.Xml;
using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Putty;
using LoipvRemote.WinUI.Hosting;
using LoipvRemote.WinUI.Services;
using LoipvRemote.WinUI.Sessions;
using LoipvRemote.WinUI.ViewModels;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace LoipvRemote.WinUI;

public sealed partial class MainWindow : Window, IDisposable
{
    private static readonly SolidColorBrush ResourceWarningBackground =
        new(Microsoft.UI.ColorHelper.FromArgb(0x26, 0xD1, 0x34, 0x38));
    private static readonly SolidColorBrush ResourceWarningForeground =
        new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xB4, 0x23, 0x18));
    private static readonly SolidColorBrush TransparentBrush =
        new(Microsoft.UI.ColorHelper.FromArgb(0, 0, 0, 0));
    private readonly ConnectionCatalog _connectionCatalog;
    private readonly ConnectionTreeViewStateRepository _treeViewStateRepository;
    private readonly PreviousSessionTabsRepository _previousSessionTabsRepository;
    private readonly ConnectionOptionsEditor _connectionOptionsEditor;
    private readonly ILocalCredentialStore _localCredentialStore;
    private readonly IConnectionSecretResolver _connectionSecretResolver;
    private readonly PortableConnectionCredentialImporter _portableCredentialImporter;
    private readonly RemoteSessionWorkspace _sessionWorkspace;
    private readonly SshFileTransferSessionFactory _sshFileTransferSessionFactory;
    private readonly DispatcherTimer _sshWorkingDirectoryTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private WindowMinimumSizeController? _minimumSizeController;
    private WindowSessionHotKeyController? _sessionHotKeyController;
    private WindowSessionPointerController? _sessionPointerController;
    private Win32EmbeddedSessionSurface? _embeddedSessionSurface;
    private readonly Dictionary<TreeViewNode, ConnectionTreeItem> _connectionNodes = [];
    private readonly Dictionary<TabViewItem, RemoteSessionTab> _sessionTabs = [];
    private readonly Dictionary<RemoteSessionTab, IRemoteResourceMonitor> _subscribedResourceMonitorTabs = [];
    private readonly HashSet<FrameworkElement> _hoveredResourceMonitorDetailChips = [];
    private readonly HashSet<Guid> _connectedConnectionIds = [];
    private readonly HashSet<Guid> _pendingRestoredConnectionIds = [];
    private readonly HashSet<Guid> _expandedFolderIds = [];
    private readonly SessionTabCloseGate _sessionTabCloseGate = new();
    private ConnectionTreeItem? _draggedTreeItem;
    private ConnectionTreeItem? _selectedTreeItem;
    private ConnectionFolderDefinition? _selectedFolder;
    private ConnectionDefinition? _selectedConnection;
    private bool _suppressSessionOpenForContextMenu;
    private bool _shutdownInProgress;
    private bool _sessionShutdownCompleted;
    private bool _restoringPreviousTabs;
    private PreviousSessionTabsState _previousSessionTabsState = PreviousSessionTabsState.Empty;
    private PreviousSessionTabsState _shutdownSessionTabsSnapshot = PreviousSessionTabsState.Empty;
    private int _xamlPopupDepth;
    private bool _titleBarRegionUpdateQueued;
    private bool _windowWasDeactivated;
    private bool _windowMinimized;
    private CancellationTokenSource? _hotKeyDeactivationCancellation;
    private int _pendingSessionTabNavigation;
    private int _sessionTabNavigationQueued;
    private long _embeddedFocusRestoreSuppressedUntil;
    private bool _disposed;
    private bool _sftpDialogOpen;
    private int _nextLocalTerminalSequence = 1;

    public MainWindow(
        ConnectionCatalog connectionCatalog,
        ConnectionTreeViewStateRepository treeViewStateRepository,
        PreviousSessionTabsRepository previousSessionTabsRepository,
        ConnectionOptionsEditor connectionOptionsEditor,
        ILocalCredentialStore localCredentialStore,
        IConnectionSecretResolver connectionSecretResolver,
        PortableConnectionCredentialImporter portableCredentialImporter,
        RemoteSessionWorkspace sessionWorkspace)
    {
        _connectionCatalog = connectionCatalog ?? throw new ArgumentNullException(nameof(connectionCatalog));
        _treeViewStateRepository = treeViewStateRepository ?? throw new ArgumentNullException(nameof(treeViewStateRepository));
        _previousSessionTabsRepository = previousSessionTabsRepository ?? throw new ArgumentNullException(nameof(previousSessionTabsRepository));
        _connectionOptionsEditor = connectionOptionsEditor ?? throw new ArgumentNullException(nameof(connectionOptionsEditor));
        _localCredentialStore = localCredentialStore ?? throw new ArgumentNullException(nameof(localCredentialStore));
        _connectionSecretResolver = connectionSecretResolver ?? throw new ArgumentNullException(nameof(connectionSecretResolver));
        _portableCredentialImporter = portableCredentialImporter ?? throw new ArgumentNullException(nameof(portableCredentialImporter));
        _sessionWorkspace = sessionWorkspace ?? throw new ArgumentNullException(nameof(sessionWorkspace));
        _sshFileTransferSessionFactory = new(definition => _connectionSecretResolver.Resolve(definition, "Password"));
        InitializeComponent();
        WelcomeVersionText.Text = ApplicationVersionText.Current;

        _sshWorkingDirectoryTimer.Tick += SshWorkingDirectoryTimerOnTick;
        _sshWorkingDirectoryTimer.Start();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBarDragRegion);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.Maximize();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "LoipvRemote.ico"));
        TryInstallMinimumSizeController();
        TryInstallSessionHotKeyController();
        TryInstallSessionPointerController();
        AppWindow.Closing += AppWindowOnClosing;
        AppWindow.Changed += AppWindowOnChanged;
        Activated += MainWindowOnActivated;
        SessionSurface.Loaded += SessionSurfaceOnLoaded;
        Closed += MainWindowOnClosed;
        RootGrid.Loaded += RootGridOnLoaded;
        AppTitleBar.Loaded += AppTitleBarOnLoaded;
        AppTitleBar.SizeChanged += AppTitleBarOnSizeChanged;
        Sessions.Loaded += SessionsOnLoaded;
        Sessions.LayoutUpdated += SessionsOnLayoutUpdated;
    }

    private async void RootGridOnLoaded(object sender, RoutedEventArgs args)
    {
        RootGrid.Loaded -= RootGridOnLoaded;
        await LoadConnectionTreeAsync();
        await LoadPreviousSessionTabsAsync();
        AlignTabStripToMainContent();
        QueueTitleBarInteractiveRegionUpdate();
    }

    private void AppTitleBarOnLoaded(object sender, RoutedEventArgs args) => QueueTitleBarInteractiveRegionUpdate();

    private void AppTitleBarOnSizeChanged(object sender, SizeChangedEventArgs args) => QueueTitleBarInteractiveRegionUpdate();

    private void SessionsOnLoaded(object sender, RoutedEventArgs args) => QueueTitleBarInteractiveRegionUpdate();

    private void SessionsOnLayoutUpdated(object? sender, object args) => QueueTitleBarInteractiveRegionUpdate();

    private void QueueTitleBarInteractiveRegionUpdate()
    {
        if (_windowMinimized || _titleBarRegionUpdateQueued)
            return;

        _titleBarRegionUpdateQueued = true;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            _titleBarRegionUpdateQueued = false;
            UpdateTitleBarLayout();
        });
    }

    private void UpdateTitleBarLayout()
    {
        if (_windowMinimized ||
            !ExtendsContentIntoTitleBar ||
            AppTitleBar.XamlRoot is null ||
            AppTitleBar.XamlRoot.RasterizationScale <= 0)
        {
            return;
        }

        double scale = AppTitleBar.XamlRoot.RasterizationScale;
        RightTitleBarInsetColumn.Width = new GridLength(
            TitleBarLayoutMetrics.ToLogicalPixels(AppWindow.TitleBar.RightInset, scale));
    }

    private void AlignTabStripToMainContent()
    {
        // WinUI's stock TabView inserts a two-pixel scroll-button column plus
        // one pixel of ScrollContentPresenter padding before the first tab.
        // Keep the stock visual style, but remove that otherwise visible gap
        // so the first tab starts exactly at the main-content edge.
        ScrollContentPresenter? presenter = FindDescendant<ScrollContentPresenter>(Sessions);
        if (presenter is null)
            return;

        presenter.Padding = new Thickness(0);
        // The presenter itself begins after the two-pixel scroll column and
        // the built-in four-pixel ItemsPresenter header. Offset both so the
        // first real tab, not merely the underline, touches the content edge.
        presenter.Margin = new Thickness(-6, 0, 0, 0);

        // The platform template animates add/delete/reorder transitions. A
        // native session connection changes host visibility asynchronously, so
        // suppressing those decorative transitions keeps the tab strip steady.
        TabViewListView? tabList = FindDescendant<TabViewListView>(Sessions);
        if (tabList is not null)
            tabList.ItemContainerTransitions = new TransitionCollection();
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                return match;

            T? nestedMatch = FindDescendant<T>(child);
            if (nestedMatch is not null)
                return nestedMatch;
        }

        return null;
    }

    private static FrameworkElement? FindNamedDescendant(DependencyObject root, string name)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is FrameworkElement element &&
                string.Equals(element.Name, name, StringComparison.Ordinal))
            {
                return element;
            }

            FrameworkElement? nestedMatch = FindNamedDescendant(child, name);
            if (nestedMatch is not null)
                return nestedMatch;
        }

        return null;
    }

    private void TryInstallMinimumSizeController()
    {
        try
        {
            _minimumSizeController = new WindowMinimumSizeController(WindowNative.GetWindowHandle(this), 760, 520);
        }
        catch (Exception exception)
        {
            // Native window subclassing is an enhancement only. A failure here
            // must not make the entire desktop application start without a UI.
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    private async Task LoadConnectionTreeAsync()
    {
        try
        {
            ConnectionCatalogLoadResult result = await _connectionCatalog.LoadAsync();
            _expandedFolderIds.Clear();
            _expandedFolderIds.UnionWith(await _treeViewStateRepository.LoadExpandedFolderIdsAsync());
            RebuildConnectionTree(result.Tree);
            await RefreshSharedCredentialsAsync();

            ConnectionStatus.Text = result.Message;
            BindStoreSettings(_connectionCatalog.Settings);
        }
        catch (Exception exception)
        {
            ConnectionStatus.Text = $"Connections could not be loaded: {exception.Message}";
        }
    }

    private void TitleBarActionButton_PointerEntered(object sender, PointerRoutedEventArgs args) =>
        SetTitleBarActionButtonBackground(sender, "TitleBarActionButtonPointerOverBrush");

    private void TitleBarActionButton_PointerExited(object sender, PointerRoutedEventArgs args) =>
        SetTitleBarActionButtonBackground(sender, "TitleBarActionButtonNormalBrush");

    private void TitleBarActionButton_PointerPressed(object sender, PointerRoutedEventArgs args) =>
        SetTitleBarActionButtonBackground(sender, "TitleBarActionButtonPressedBrush");

    private void TitleBarActionButton_PointerReleased(object sender, PointerRoutedEventArgs args) =>
        SetTitleBarActionButtonBackground(sender, "TitleBarActionButtonPointerOverBrush");

    private void SetTitleBarActionButtonBackground(object sender, string brushKey)
    {
        if (sender is Button button &&
            TitleBarActions.Resources.TryGetValue(brushKey, out object? resource) &&
            resource is Brush brush)
        {
            button.Background = brush;
        }
    }

    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        BeginXamlPopup();
        bool restoreEmbeddedSession = HasSelectedConnectedEmbeddedSession();
        if (restoreEmbeddedSession)
        {
            // ContentDialog owns a full-window scrim. Keeping the owned native
            // popup above that scrim would expose only the dialog-card hole and
            // leave the remaining session host as a white rectangle.
            _embeddedSessionSurface?.ClearXamlOverlayOcclusions();
            _embeddedSessionSurface?.SetVisible(false);
        }

        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            EndXamlPopup();
            if (restoreEmbeddedSession && HasSelectedConnectedEmbeddedSession())
            {
                _embeddedSessionSurface?.SetVisible(true);
                _embeddedSessionSurface?.RestoreFocusAfterTransition();
            }
        }
    }

    private bool HasSelectedConnectedEmbeddedSession() =>
        !_windowMinimized &&
        Sessions.SelectedItem is TabViewItem selectedTab &&
        _sessionTabs.TryGetValue(selectedTab, out RemoteSessionTab? selectedSession) &&
        selectedSession.State == RemoteSessionTabState.Connected &&
        selectedSession.Session is IEmbeddedWindow;

    private static TextBlock CreateDialogValidationText() => new()
    {
        TextWrapping = TextWrapping.Wrap,
        Visibility = Visibility.Collapsed
    };

    private void KeepDialogOpenWhenInvalid(
        ContentDialog dialog,
        TextBlock validationText,
        Func<string?> getError,
        Func<Control> getInvalidField)
    {
        dialog.Closing += (dialogSender, eventArgs) =>
        {
            if (eventArgs.Result != ContentDialogResult.Primary)
                return;

            string? error = getError();
            if (error is null)
                return;

            eventArgs.Cancel = true;
            validationText.Text = error;
            validationText.Visibility = Visibility.Visible;
            _ = DispatcherQueue.TryEnqueue(() => getInvalidField().Focus(FocusState.Programmatic));
        };
    }

    private void XamlPopup_Opening(object? sender, object args) => BeginXamlPopup();

    private void XamlPopup_Opened(object? sender, object args) => QueueXamlPopupOcclusionUpdate();

    private void XamlPopup_Closed(object? sender, object args) => EndXamlPopup();

    private void BeginXamlPopup()
    {
        _xamlPopupDepth++;
        CancelEmbeddedSessionFocusRestore();
    }

    private void EndXamlPopup()
    {
        if (_xamlPopupDepth == 0 || --_xamlPopupDepth != 0)
            return;

        _embeddedSessionSurface?.ClearXamlOverlayOcclusions();
        if (HasSelectedConnectedEmbeddedSession())
        {
            _embeddedSessionSurface?.RestoreFocusAfterTransition();
        }
    }

    private void QueueXamlPopupOcclusionUpdate()
    {
        _ = DispatcherQueue.TryEnqueue(UpdateXamlPopupOcclusions);
    }

    private void UpdateXamlPopupOcclusions()
    {
        if (_xamlPopupDepth == 0 ||
            _embeddedSessionSurface is null ||
            RootGrid.XamlRoot is null)
        {
            return;
        }

        var overlays = new List<FrameworkElement>();
        foreach (Popup popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(RootGrid.XamlRoot))
        {
            if (popup.Child is not FrameworkElement child)
                continue;

            MenuFlyoutPresenter? presenter = child as MenuFlyoutPresenter ??
                FindDescendant<MenuFlyoutPresenter>(child);
            if (presenter is not null)
                overlays.Add(presenter);
        }

        _ = NativeUiExceptionGuard.TryRun(
            () => _embeddedSessionSurface.SetXamlOverlayOcclusions(overlays),
            exception => EmbeddingDiagnostics.Write(
                $"xaml-overlay-occlusion-recovered type={exception.GetType().Name} hresult=0x{exception.HResult:X8}"));
    }

    private bool CanEditConnectionStore()
    {
        if (_connectionCatalog.IsLoaded && !_connectionCatalog.Settings.IsReadOnly)
            return true;

        ConnectionStatus.Text = _connectionCatalog.IsLoaded
            ? "The selected connection store is read-only."
            : "Open a writable connection store in Config before editing.";
        return false;
    }
    private async void NewConnectionButton_Click(object sender, RoutedEventArgs args)
    {
        if (!CanEditConnectionStore())
            return;

        var nameBox = new TextBox { Header = "Name", PlaceholderText = "Production server" };
        var hostBox = new TextBox { Header = "Host", PlaceholderText = "host.example.com" };
        var portBox = new NumberBox { Header = "Port", Value = 22, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var protocolBox = new ComboBox { Header = "Protocol", ItemsSource = Enum.GetValues<ProtocolKind>() };
        var usernameBox = new TextBox { Header = "Username" };
        var passwordBox = new PasswordBox { Header = "Password" };
        ComboBox credentialBox = await CreateCredentialBoxAsync(CredentialReference.None);
        protocolBox.SelectedItem = ProtocolKind.Ssh2;
        var protocolFields = new ConnectionProtocolFields(this);
        protocolFields.Bind(protocolBox, portBox);
        TextBlock validationText = CreateDialogValidationText();
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "New connection",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel
            {
                MinWidth = 400,
                Spacing = 10,
                Children = { validationText, CreateConnectionFieldRow(nameBox, protocolBox), CreateConnectionFieldRow(hostBox, portBox), usernameBox, passwordBox, credentialBox, protocolFields.View }
            }
        };

        KeepDialogOpenWhenInvalid(
            dialog,
            validationText,
            () => ConnectionDialogValidation.GetError(
                nameBox.Text,
                hostBox.Text,
                protocolBox.SelectedItem is ProtocolKind selectedProtocol ? selectedProtocol : null,
                portBox.Value),
            () => string.IsNullOrWhiteSpace(nameBox.Text)
                ? nameBox
                : string.IsNullOrWhiteSpace(hostBox.Text)
                    ? hostBox
                    : protocolBox.SelectedItem is not ProtocolKind
                        ? protocolBox
                        : portBox);

        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
            return;

        if (protocolBox.SelectedItem is not ProtocolKind protocol ||
            double.IsNaN(portBox.Value) || portBox.Value is < 1 or > 65535)
        {
            ConnectionStatus.Text = "Enter a valid protocol and port between 1 and 65535.";
            return;
        }

        try
        {
            Guid connectionId = Guid.NewGuid();
            ConnectionNodeOptions? options = _connectionOptionsEditor.BuildFromFields(
                connectionId,
                protocolFields.ReadOptions(protocol),
                passwordBox.Password,
                clearStoredPassword: false);
            options = ConnectionOptionsEditor.WithOptionValue(options, "Username", usernameBox.Text);
            CredentialChoice credentialChoice = SelectedCredentialChoice(credentialBox);
            options = ApplyCredentialUserName(options, credentialChoice);
            ConnectionTreeDefinition updated = ConnectionTreeEditor.AddConnection(
                _connectionCatalog.Tree,
                nameBox.Text,
                hostBox.Text,
                (int)portBox.Value,
                protocol,
                parentFolderId: _selectedFolder?.Id,
                options: options,
                connectionId: connectionId,
                credential: credentialChoice.Reference);
            await _connectionCatalog.SaveAsync(updated);
            await LoadConnectionTreeAsync();
            ConnectionStatus.Text = "Connection saved.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or InvalidOperationException or CryptographicException or FormatException)
        {
            ConnectionStatus.Text = $"Connection was not saved: {exception.Message}";
        }
    }

    private static Grid CreateConnectionFieldRow(Control primary, Control secondary)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        primary.HorizontalAlignment = HorizontalAlignment.Stretch;
        secondary.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(secondary, 1);
        row.Children.Add(primary);
        row.Children.Add(secondary);
        return row;
    }

    private async void QuickConnectButton_Click(object sender, RoutedEventArgs args)
    {
        var hostBox = new TextBox { Header = "Host", PlaceholderText = "host.example.com" };
        var portBox = new NumberBox { Header = "Port", Value = 22, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var protocolBox = new ComboBox { Header = "Protocol", ItemsSource = Enum.GetValues<ProtocolKind>(), SelectedItem = ProtocolKind.Ssh2 };
        var usernameBox = new TextBox { Header = "Username (optional)" };
        var passwordBox = new PasswordBox { Header = "Password (only for this session)" };
        TextBlock validationText = CreateDialogValidationText();
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Quick connect",
            PrimaryButtonText = "Connect",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel { Spacing = 12, Children = { validationText, hostBox, portBox, protocolBox, usernameBox, passwordBox } }
        };
        KeepDialogOpenWhenInvalid(
            dialog,
            validationText,
            () => ConnectionDialogValidation.GetQuickConnectError(
                hostBox.Text,
                protocolBox.SelectedItem is ProtocolKind selectedProtocol ? selectedProtocol : null,
                portBox.Value),
            () => string.IsNullOrWhiteSpace(hostBox.Text)
                ? hostBox
                : protocolBox.SelectedItem is not ProtocolKind
                    ? protocolBox
                    : portBox);
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
            return;
        if (protocolBox.SelectedItem is not ProtocolKind protocol ||
            string.IsNullOrWhiteSpace(hostBox.Text) ||
            double.IsNaN(portBox.Value) || portBox.Value is < 1 or > 65535)
        {
            ConnectionStatus.Text = "Enter a host, supported protocol, and port between 1 and 65535.";
            return;
        }

        ConnectionDefinition definition = _connectionOptionsEditor.CreateQuickConnection(
            hostBox.Text,
            (int)portBox.Value,
            protocol,
            usernameBox.Text,
            passwordBox.Password);
        RemoteSessionTab tab = _sessionWorkspace.Open(definition);
        TabViewItem tabItem = CreateSessionTabItem(tab);
        _sessionTabs.Add(tabItem, tab);
        Sessions.TabItems.Add(tabItem);
        Sessions.SelectedItem = tabItem;
        ShowSession(tab);
        QueueTitleBarInteractiveRegionUpdate();
        await ConnectSessionAsync(tab);
    }

    private async Task<ComboBox> CreateCredentialBoxAsync(LoipvRemote.Domain.Credentials.CredentialReference selected)
    {
        IReadOnlyList<LocalCredentialDefinition> credentials = await _localCredentialStore.ListAsync();
        var choices = new List<CredentialChoice> { new(LoipvRemote.Domain.Credentials.CredentialReference.None, "No shared credential") };
        choices.AddRange(credentials
            .OrderBy(credential => credential.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(credential => new CredentialChoice(
                LoipvRemote.Domain.Credentials.CredentialReference.LocalDpapi(credential.Id),
                string.IsNullOrWhiteSpace(credential.UserName) ? credential.Name : $"{credential.Name} ({credential.UserName})",
                credential.UserName)));
        var box = new ComboBox { Header = "Shared credential (optional)", ItemsSource = choices, DisplayMemberPath = nameof(CredentialChoice.Name) };
        box.SelectedItem = choices.FirstOrDefault(choice => choice.Reference == selected) ?? choices[0];
        return box;
    }

    private static CredentialChoice SelectedCredentialChoice(ComboBox credentialBox) =>
        credentialBox.SelectedItem as CredentialChoice ?? new CredentialChoice(LoipvRemote.Domain.Credentials.CredentialReference.None, "No shared credential");

    private static ConnectionNodeOptions? ApplyCredentialUserName(ConnectionNodeOptions? options, CredentialChoice credential)
    {
        if (string.IsNullOrWhiteSpace(credential.UserName))
            return options;
        var values = options is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(options.Values, StringComparer.Ordinal);
        values["Username"] = credential.UserName;
        return new ConnectionNodeOptions(values, options?.InheritedProperties.ToArray() ?? []);
    }

    private async Task<Dictionary<Guid, PortableConnectionCredential>> CreatePortableCredentialsAsync()
    {
        // ResolvePassword is synchronous, so prime the local credential cache
        // before collecting passwords for a portable export.
        _ = await _localCredentialStore.ListAsync();
        var credentials = new Dictionary<Guid, PortableConnectionCredential>();
        foreach (ConnectionDefinition connection in _connectionCatalog.Tree.Connections)
        {
            ConnectionDefinition effective = _connectionCatalog.FindResolvedConnection(connection.Id) ?? connection;
            string userName = effective.Options?.Values.TryGetValue("Username", out string? configuredUserName) == true
                ? configuredUserName
                : string.Empty;
            credentials.Add(
                connection.Id,
                new PortableConnectionCredential(
                    userName,
                    _connectionSecretResolver.Resolve(effective, "Password") ?? string.Empty,
                    _connectionSecretResolver.Resolve(effective, "RDGatewayPassword") ?? string.Empty));
        }

        return credentials;
    }

    private async void NewFolderButton_Click(object sender, RoutedEventArgs args)
    {
        if (!CanEditConnectionStore())
            return;

        var nameBox = new TextBox { Header = "Folder name", PlaceholderText = "Production" };
        TextBlock validationText = CreateDialogValidationText();
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "New folder",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel { Spacing = 12, Children = { validationText, nameBox } }
        };
        KeepDialogOpenWhenInvalid(
            dialog,
            validationText,
            () => ConnectionDialogValidation.GetFolderError(nameBox.Text),
            () => nameBox);

        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
            return;

        try
        {
            await _connectionCatalog.SaveAsync(ConnectionTreeEditor.AddFolder(_connectionCatalog.Tree, nameBox.Text, _selectedFolder?.Id));
            await LoadConnectionTreeAsync();
            ConnectionStatus.Text = "Folder saved.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException or FormatException)
        {
            ConnectionStatus.Text = $"Folder was not saved: {exception.Message}";
        }
    }

    private async void ImportConnectionsButton_Click(object sender, RoutedEventArgs args)
    {
        if (!CanEditConnectionStore())
            return;

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".xml");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        Windows.Storage.StorageFile? source = await picker.PickSingleFileAsync();
        if (source is null)
            return;

        try
        {
            ConnectionExportPackage package = await new XmlConnectionDefinitionStore(source.Path).LoadPortableAsync();
            string importName = Path.GetFileNameWithoutExtension(source.Name);
            ConnectionTreeImportResult mergeResult = ConnectionTreeEditor.MergeImportedTreeWithIdMap(
                _connectionCatalog.Tree,
                package.Tree,
                importName);
            ConnectionTreeDefinition updated = _portableCredentialImporter.Apply(
                mergeResult.Tree,
                mergeResult.ConnectionIds,
                package.Credentials);
            await _connectionCatalog.SaveAsync(updated);
            await LoadConnectionTreeAsync();
            ConnectionStatus.Text = $"Imported {package.Tree.Connections.Count} connections with their credentials.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or System.Xml.XmlException or CryptographicException)
        {
            ConnectionStatus.Text = $"Import failed: {exception.Message}";
        }
    }

    private async void ExportConnectionsButton_Click(object sender, RoutedEventArgs args)
    {
        var confirmation = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Export plaintext credentials?",
            Content = PortableExportPolicy.GetWarning(_connectionCatalog.Tree.Connections.Count),
            PrimaryButtonText = "Export",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await ShowDialogAsync(confirmation) != ContentDialogResult.Primary)
            return;

        var picker = new FileSavePicker { SuggestedFileName = "LoipvRemote-connections" };
        picker.FileTypeChoices.Add("LoipvRemote XML", [".xml"]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        Windows.Storage.StorageFile? destination = await picker.PickSaveFileAsync();
        if (destination is null)
            return;

        try
        {
            await new XmlConnectionDefinitionStore(destination.Path).SavePortableAsync(
                _connectionCatalog.Tree,
                await CreatePortableCredentialsAsync());
            ConnectionStatus.Text = $"Exported {_connectionCatalog.Tree.Connections.Count} connections with plaintext credentials.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or CryptographicException)
        {
            ConnectionStatus.Text = $"Export failed: {exception.Message}";
        }
    }

    private async void EditSelectedNodeButton_Click(object sender, RoutedEventArgs args)
    {
        if (!CanEditConnectionStore())
            return;

        if (_selectedFolder is not null)
        {
            await EditFolderAsync(_selectedFolder);
            return;
        }
        if (_selectedConnection is null)
        {
            ConnectionStatus.Text = "Select a folder or connection before editing it.";
            return;
        }

        ConnectionDefinition connection = _selectedConnection;
        var nameBox = new TextBox { Header = "Name", Text = connection.Name };
        var hostBox = new TextBox { Header = "Host", Text = connection.Host };
        var portBox = new NumberBox { Header = "Port", Value = connection.Port, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var protocolBox = new ComboBox { Header = "Protocol", ItemsSource = Enum.GetValues<ProtocolKind>(), SelectedItem = connection.Protocol };
        var usernameBox = new TextBox
        {
            Header = "Username",
            Text = connection.Options?.Values.TryGetValue("Username", out string? username) == true ? username : string.Empty
        };
        var passwordBox = new PasswordBox { Header = "Password", PlaceholderText = "Leave blank to keep the saved password" };
        var clearPasswordBox = new CheckBox { Content = "Clear stored password" };
        ComboBox credentialBox = await CreateCredentialBoxAsync(connection.Credential);
        var protocolFields = new ConnectionProtocolFields(this, connection.Options,
            _connectionCatalog.FindResolvedConnection(connection.Id)?.Options);
        protocolFields.Bind(protocolBox, portBox);
        TextBlock validationText = CreateDialogValidationText();
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Edit connection",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel
            {
                MinWidth = 400,
                Spacing = 10,
                Children = { validationText, CreateConnectionFieldRow(nameBox, protocolBox), CreateConnectionFieldRow(hostBox, portBox), usernameBox, passwordBox, clearPasswordBox, credentialBox, protocolFields.View }
            }
        };
        KeepDialogOpenWhenInvalid(
            dialog,
            validationText,
            () => ConnectionDialogValidation.GetError(
                nameBox.Text,
                hostBox.Text,
                protocolBox.SelectedItem is ProtocolKind selectedProtocol ? selectedProtocol : null,
                portBox.Value),
            () => string.IsNullOrWhiteSpace(nameBox.Text)
                ? nameBox
                : string.IsNullOrWhiteSpace(hostBox.Text)
                    ? hostBox
                    : protocolBox.SelectedItem is not ProtocolKind
                        ? protocolBox
                        : portBox);

        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
            return;
        if (protocolBox.SelectedItem is not ProtocolKind protocol || double.IsNaN(portBox.Value) || portBox.Value is < 1 or > 65535)
        {
            ConnectionStatus.Text = "Enter a port between 1 and 65535.";
            return;
        }

        try
        {
            ConnectionNodeOptions? options = _connectionOptionsEditor.BuildFromFields(
                connection.Id,
                protocolFields.ReadOptions(protocol),
                passwordBox.Password,
                clearPasswordBox.IsChecked == true);
            options = ConnectionOptionsEditor.WithOptionValue(options, "Username", usernameBox.Text);
            CredentialChoice credentialChoice = SelectedCredentialChoice(credentialBox);
            options = ApplyCredentialUserName(options, credentialChoice);
            ConnectionTreeDefinition updated = ConnectionTreeEditor.UpdateConnection(
                _connectionCatalog.Tree,
                connection.Id,
                nameBox.Text,
                hostBox.Text,
                (int)portBox.Value,
                options,
                credentialChoice.Reference,
                protocol);
            await _connectionCatalog.SaveAsync(updated);
            await CloseSessionTabsForConnectionAsync(connection.Id);
            _selectedConnection = null;
            await LoadConnectionTreeAsync();
            ConnectionStatus.Text = "Connection saved. Existing session tabs were closed.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException or FormatException)
        {
            ConnectionStatus.Text = $"Connection was not saved: {exception.Message}";
        }
    }

    private async Task EditFolderAsync(ConnectionFolderDefinition folder)
    {
        var nameBox = new TextBox { Header = "Folder name", Text = folder.Name };
        var optionsBox = new TextBox
        {
            Header = "Inherited options (Name=Value per line)",
            Text = ConnectionOptionsEditor.Format(folder.Options),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 72
        };
        TextBlock validationText = CreateDialogValidationText();
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = folder.IsRoot ? "Edit root folder" : "Edit folder",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel { Spacing = 12, Children = { validationText, nameBox, optionsBox } }
        };
        KeepDialogOpenWhenInvalid(
            dialog,
            validationText,
            () => ConnectionDialogValidation.GetFolderError(nameBox.Text),
            () => nameBox);
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
            return;

        try
        {
            // Folders can contribute only non-secret options to descendants.
            // Credentials remain explicit connection or shared-DPAPI references.
            ConnectionNodeOptions? options = _connectionOptionsEditor.Build(
                folder.Id,
                optionsBox.Text,
                password: null,
                clearStoredPassword: false);
            await _connectionCatalog.SaveAsync(ConnectionTreeEditor.UpdateFolder(
                _connectionCatalog.Tree,
                folder.Id,
                nameBox.Text,
                options));
            _selectedFolder = null;
            await LoadConnectionTreeAsync();
            ConnectionStatus.Text = "Folder saved. Its options apply to descendant connections unless overridden.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException or FormatException)
        {
            ConnectionStatus.Text = $"Folder was not saved: {exception.Message}";
        }
    }

    private TreeViewNode CreateTreeNode(ConnectionTreeItem item)
    {
        TreeViewNode node = new()
        {
            Content = item,
            IsExpanded = item.IsFolder && _expandedFolderIds.Contains(item.Id)
        };

        foreach (ConnectionTreeItem child in item.Children)
            node.Children.Add(CreateTreeNode(child));

        _connectionNodes.Add(node, item);
        return node;
    }

    private void SessionSurfaceOnLoaded(object sender, RoutedEventArgs args)
    {
        _embeddedSessionSurface ??= new Win32EmbeddedSessionSurface(this, NativeSessionViewport);
    }

    private void AppWindowOnChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        _windowMinimized = sender.Presenter is OverlappedPresenter
        {
            State: OverlappedPresenterState.Minimized
        };
        bool shouldRefreshNativeSession = WindowTransitionPolicy.ShouldRefreshNativeSession(
            _windowMinimized,
            args.DidPositionChange,
            args.DidSizeChange,
            args.DidPresenterChange);
        if (_windowMinimized)
        {
            _embeddedSessionSurface?.SuspendForMinimize();
            return;
        }

        if (shouldRefreshNativeSession)
        {
            if (IsEmbeddedFocusRestoreSuppressed())
                _embeddedSessionSurface?.RefreshLayoutAfterWindowTransition();
            else
                _embeddedSessionSurface?.RefreshLayoutAndRestoreFocus();
        }

        if (args.DidSizeChange || args.DidPresenterChange)
            QueueTitleBarInteractiveRegionUpdate();
    }

    private void MainWindowOnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            QueueSessionHotKeyDeactivationCheck();
            _windowWasDeactivated = true;
            return;
        }

        _hotKeyDeactivationCancellation?.Cancel();
        _hotKeyDeactivationCancellation?.Dispose();
        _hotKeyDeactivationCancellation = null;
        SetSessionHotKeysEnabled(true);

        // Window.Activated can be raised more than once while the native
        // child processes a focus message. Restore focus only for a real
        // return from another application, never for an already-active app.
        if (!_windowWasDeactivated)
            return;

        _windowWasDeactivated = false;
        if (_xamlPopupDepth == 0 && !IsEmbeddedFocusRestoreSuppressed())
            _embeddedSessionSurface?.RestoreFocusAfterTransition();
        QueueSessionFocusAfterWindowActivation();
    }

    private void QueueSessionFocusAfterWindowActivation()
    {
        // Chờ thao tác caption hoàn tất rồi khôi phục focus khi quay lại app.
        // Dialog hoặc lần click mới vào shell phải giữ quyền nhận bàn phím.
        _ = DispatcherQueue.TryEnqueue(async () =>
        {
            var remaining = Volatile.Read(ref _embeddedFocusRestoreSuppressedUntil) - Environment.TickCount64;
            if (remaining > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(remaining));
            if (_disposed || _windowMinimized || _xamlPopupDepth > 0 || IsEmbeddedFocusRestoreSuppressed())
                return;

            _embeddedSessionSurface?.RestoreFocusAfterTransition();
        });
    }

    private void SetSessionHotKeysEnabled(bool enabled)
    {
        try
        {
            _sessionHotKeyController?.SetEnabled(enabled);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    private void QueueSessionHotKeyDeactivationCheck()
    {
        _hotKeyDeactivationCancellation?.Cancel();
        _hotKeyDeactivationCancellation?.Dispose();
        CancellationTokenSource cancellation = _hotKeyDeactivationCancellation = new();
        _ = DisableSessionHotKeysIfFocusLeftAppAsync(cancellation.Token);
    }

    private async Task DisableSessionHotKeysIfFocusLeftAppAsync(CancellationToken cancellationToken)
    {
        try
        {
            // PuTTY is a child HWND owned by another process, so WinUI may
            // briefly report Deactivated while the user is still inside this
            // app. Let foreground ownership settle before unregistering.
            await Task.Delay(75, cancellationToken);
            if (!cancellationToken.IsCancellationRequested &&
                _sessionHotKeyController is { } controller &&
                !controller.HasForegroundOwnership())
            {
                SetSessionHotKeysEnabled(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The app became active again before the ownership check settled.
        }
    }

    private void MainWindowOnClosed(object sender, WindowEventArgs args)
    {
        try
        {
            if (!_sessionShutdownCompleted)
                _sessionWorkspace.CloseAllAsync().GetAwaiter().GetResult();
        }
        finally
        {
            Dispose();
        }
    }

    private void AppWindowOnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_sessionShutdownCompleted)
            return;

        args.Cancel = true;
        if (_shutdownInProgress)
            return;

        _shutdownInProgress = true;
        _shutdownSessionTabsSnapshot = CapturePreviousSessionTabs();
        StopInteractiveSessionInputForShutdown();
        try
        {
            // Session teardown can take several seconds, especially when a
            // remote endpoint is no longer responding. The first close click
            // must still dismiss the shell immediately.
            sender.Hide();
        }
        catch (Exception exception)
        {
            EmbeddingDiagnostics.Write(
                $"window-hide-during-shutdown-failed type={exception.GetType().Name} hresult=0x{exception.HResult:X8}");
        }
        _ = CloseAfterSessionCleanupAsync();
    }

    private void StopInteractiveSessionInputForShutdown()
    {
        Activated -= MainWindowOnActivated;
        _hotKeyDeactivationCancellation?.Cancel();
        _hotKeyDeactivationCancellation?.Dispose();
        _hotKeyDeactivationCancellation = null;
        _sessionHotKeyController?.Dispose();
        _sessionHotKeyController = null;
        _sessionPointerController?.Dispose();
        _sessionPointerController = null;
        RemoteSessionWorkspace.Deactivate(_embeddedSessionSurface);
    }

    private async Task CloseAfterSessionCleanupAsync()
    {
        try
        {
            await _previousSessionTabsRepository.SaveAsync(_shutdownSessionTabsSnapshot);
        }
        catch (Exception exception)
        {
            EmbeddingDiagnostics.Write(
                $"previous-session-tabs-save-failed type={exception.GetType().Name} hresult=0x{exception.HResult:X8}");
        }

        try
        {
            // Let WinUI commit AppWindow.Hide before any native protocol
            // shutdown path can synchronously wait for a child process.
            await Task.Yield();
            await _sessionWorkspace.CloseAllAsync();
        }
        catch (Exception)
        {
            // All sessions were attempted. The window must still close so a
            // single protocol teardown failure cannot leave the app resident.
        }
        finally
        {
            _sessionShutdownCompleted = true;
            Dispose();
            Close();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _sshWorkingDirectoryTimer.Stop();
        _sshWorkingDirectoryTimer.Tick -= SshWorkingDirectoryTimerOnTick;
        _subscribedResourceMonitorTabs.Clear();
        _embeddedSessionSurface?.Dispose();
        _embeddedSessionSurface = null;
        AppWindow.Changed -= AppWindowOnChanged;
        Activated -= MainWindowOnActivated;
        AppTitleBar.Loaded -= AppTitleBarOnLoaded;
        AppTitleBar.SizeChanged -= AppTitleBarOnSizeChanged;
        Sessions.Loaded -= SessionsOnLoaded;
        Sessions.LayoutUpdated -= SessionsOnLayoutUpdated;
        _hotKeyDeactivationCancellation?.Cancel();
        _hotKeyDeactivationCancellation?.Dispose();
        _hotKeyDeactivationCancellation = null;
        _minimumSizeController?.Dispose();
        _sessionHotKeyController?.Dispose();
        _sessionHotKeyController = null;
        _sessionPointerController?.Dispose();
        _sessionPointerController = null;
        GC.SuppressFinalize(this);
    }

    private void ConnectionsNavigationButton_Click(object sender, RoutedEventArgs args)
    {
        RemoteSessionWorkspace.Deactivate(_embeddedSessionSurface);
        SetResourceMonitoringActive(null);
        ResourceMonitorBar.Visibility = Visibility.Collapsed;
        HideManagedSession();
        WelcomeContent.Visibility = Visibility.Visible;
        ConfigContent.Visibility = Visibility.Collapsed;
        SessionContent.Visibility = Visibility.Collapsed;
    }

    private async Task LoadPreviousSessionTabsAsync()
    {
        PreviousSessionTabsState savedState = await _previousSessionTabsRepository.LoadAsync();
        _previousSessionTabsState = PreviousSessionTabsState.Create(
            savedState.ConnectionIds.Where(id => _connectionCatalog.FindResolvedConnection(id) is not null),
            savedState.SelectedConnectionId);
        RestorePreviousTabsButton.Visibility = _previousSessionTabsState.ConnectionIds.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private PreviousSessionTabsState CapturePreviousSessionTabs()
    {
        Guid[] connectionIds = Sessions.TabItems
            .OfType<TabViewItem>()
            .Where(_sessionTabs.ContainsKey)
            .Select(tab => _sessionTabs[tab].Connection.Id)
            .Where(id => _connectionCatalog.FindConnection(id) is not null)
            .ToArray();
        Guid? selectedConnectionId = Sessions.SelectedItem is TabViewItem selectedTab &&
            _sessionTabs.TryGetValue(selectedTab, out RemoteSessionTab? selectedSession) &&
            _connectionCatalog.FindConnection(selectedSession.Connection.Id) is not null
                ? selectedSession.Connection.Id
                : null;
        return PreviousSessionTabsState.Create(connectionIds, selectedConnectionId);
    }

    private void RestorePreviousTabsButton_Click(object sender, RoutedEventArgs args)
    {
        RestorePreviousTabsButton.IsEnabled = false;
        _restoringPreviousTabs = true;
        var restoredTabs = new List<(TabViewItem Item, RemoteSessionTab Session)>();
        try
        {
            foreach (Guid connectionId in _previousSessionTabsState.ConnectionIds)
            {
                ConnectionDefinition? definition = _connectionCatalog.FindResolvedConnection(connectionId);
                if (definition is null)
                    continue;

                RemoteSessionTab session = _sessionWorkspace.Open(definition);
                TabViewItem? item = _sessionTabs
                    .FirstOrDefault(pair => ReferenceEquals(pair.Value, session))
                    .Key;
                if (item is null)
                {
                    item = CreateSessionTabItem(session);
                    _sessionTabs.Add(item, session);
                    if (session.State is RemoteSessionTabState.Created or RemoteSessionTabState.Faulted)
                        _pendingRestoredConnectionIds.Add(session.Connection.Id);
                    Sessions.TabItems.Add(item);
                }
                restoredTabs.Add((item, session));
            }
        }
        finally
        {
            _restoringPreviousTabs = false;
            RestorePreviousTabsButton.IsEnabled = true;
            RestorePreviousTabsButton.Visibility = Visibility.Collapsed;
        }

        if (restoredTabs.Count == 0)
        {
            ConnectionStatus.Text = "Previous tabs are no longer available.";
            return;
        }

        Guid targetConnectionId = _previousSessionTabsState.SelectedConnectionId ??
            restoredTabs[0].Session.Connection.Id;
        (TabViewItem Item, RemoteSessionTab Session) target = restoredTabs
            .FirstOrDefault(pair => pair.Session.Connection.Id == targetConnectionId);
        if (target.Item is null)
            target = restoredTabs[0];

        Sessions.SelectedItem = target.Item;
        ShowSession(target.Session);
        TryConnectRestoredSession(target.Session);
        QueueTitleBarInteractiveRegionUpdate();
        ConnectionStatus.Text = $"Restored {restoredTabs.Count} previous tab(s).";
        _previousSessionTabsState = PreviousSessionTabsState.Empty;
    }

    private void TryConnectRestoredSession(RemoteSessionTab sessionTab)
    {
        if (_restoringPreviousTabs ||
            !_pendingRestoredConnectionIds.Remove(sessionTab.Connection.Id) ||
            sessionTab.State is not (RemoteSessionTabState.Created or RemoteSessionTabState.Faulted))
        {
            return;
        }

        _ = ConnectSessionAsync(sessionTab);
    }

    private void ConfigNavigationButton_Click(object sender, RoutedEventArgs args)
    {
        RemoteSessionWorkspace.Deactivate(_embeddedSessionSurface);
        SetResourceMonitoringActive(null);
        ResourceMonitorBar.Visibility = Visibility.Collapsed;
        HideManagedSession();
        WelcomeContent.Visibility = Visibility.Collapsed;
        ConfigContent.Visibility = Visibility.Visible;
        SessionContent.Visibility = Visibility.Collapsed;
        BindStoreSettings(_connectionCatalog.IsLoaded
            ? _connectionCatalog.Settings
            : ConnectionStoreSettings.CreateDefault());
        _ = RefreshSharedCredentialsAsync();
    }

    private async void OpenStoreButton_Click(object sender, RoutedEventArgs args) =>
        await ChangeStoreAsync(migrateCurrentTree: false);

    private async void MigrateStoreButton_Click(object sender, RoutedEventArgs args)
    {
        var confirmation = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Migrate current connections?",
            Content = "This replaces the selected store with the current connection tree. The previous store is left unchanged.",
            PrimaryButtonText = "Migrate",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await ShowDialogAsync(confirmation) == ContentDialogResult.Primary)
            await ChangeStoreAsync(migrateCurrentTree: true);
    }

    private async Task ChangeStoreAsync(bool migrateCurrentTree)
    {
        if (StoreKindBox.SelectedItem is not ConnectionDefinitionStoreKind kind)
        {
            StoreConfigurationStatus.Text = "Select a storage backend.";
            return;
        }

        AppThemeMode theme = ThemeBox.SelectedItem is AppThemeMode selectedTheme ? selectedTheme : AppThemeMode.System;
        var settings = new ConnectionStoreSettings(kind, StoreLocationBox.Text, StoreReadOnlyToggle.IsOn, theme);
        try
        {
            ConnectionCatalogLoadResult result = await _connectionCatalog.ChangeStoreAsync(settings, migrateCurrentTree);
            RebuildConnectionTree(result.Tree);
            StoreConfigurationStatus.Text = migrateCurrentTree
                ? $"Migrated {result.Tree.Connections.Count} connections to {settings.Kind}."
                : result.Message;
            ConnectionStatus.Text = result.Message;
            _selectedConnection = null;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or System.Xml.XmlException or System.Data.Common.DbException)
        {
            StoreConfigurationStatus.Text = $"Storage change failed: {exception.Message}";
        }
    }

    private void BindStoreSettings(ConnectionStoreSettings settings)
    {
        StoreKindBox.ItemsSource = Enum.GetValues<ConnectionDefinitionStoreKind>();
        StoreKindBox.SelectedItem = settings.Kind;
        ThemeBox.ItemsSource = Enum.GetValues<AppThemeMode>();
        ThemeBox.SelectedItem = settings.Theme;
        ApplyTheme(settings.Theme);
        StoreLocationBox.Text = settings.Location;
        StoreReadOnlyToggle.IsOn = settings.IsReadOnly;
        StoreConfigurationStatus.Text = $"Using {settings.Kind} storage.";
    }

    private async void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_connectionCatalog.IsLoaded || ThemeBox.SelectedItem is not AppThemeMode theme)
            return;

        ApplyTheme(theme);
        try
        {
            await _connectionCatalog.ChangeStoreAsync(_connectionCatalog.Settings with { Theme = theme }, migrateCurrentTree: false);
            StoreConfigurationStatus.Text = $"Theme set to {theme}.";
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or InvalidDataException or System.Xml.XmlException or System.Data.Common.DbException)
        {
            StoreConfigurationStatus.Text = $"Theme could not be saved: {exception.Message}";
        }
    }

    private void ApplyTheme(AppThemeMode theme) => RootGrid.RequestedTheme = theme switch
    {
        AppThemeMode.Light => ElementTheme.Light,
        AppThemeMode.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    private async void AddSharedCredentialButton_Click(object sender, RoutedEventArgs args)
    {
        var nameBox = new TextBox { Header = "Credential name", PlaceholderText = "Production administrator" };
        var userNameBox = new TextBox { Header = "Username", PlaceholderText = "administrator" };
        var passwordBox = new PasswordBox { Header = "Password" };
        TextBlock validationText = CreateDialogValidationText();
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Add shared credential",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel { Spacing = 12, Children = { validationText, nameBox, userNameBox, passwordBox } }
        };
        KeepDialogOpenWhenInvalid(
            dialog,
            validationText,
            () => ConnectionDialogValidation.GetCredentialError(nameBox.Text, passwordBox.Password),
            () => string.IsNullOrWhiteSpace(nameBox.Text) ? nameBox : passwordBox);
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
            return;

        try
        {
            await _localCredentialStore.SaveAsync(
                new LocalCredentialDefinition(Guid.NewGuid(), nameBox.Text, userNameBox.Text),
                passwordBox.Password);
            await RefreshSharedCredentialsAsync();
            CredentialConfigurationStatus.Text = "Shared credential saved. Select it in a connection editor.";
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or CryptographicException)
        {
            CredentialConfigurationStatus.Text = $"Credential was not saved: {exception.Message}";
        }
    }

    private async void EditSharedCredentialButton_Click(object sender, RoutedEventArgs args)
    {
        if (SharedCredentialList.SelectedItem is not CredentialListItem selected)
        {
            CredentialConfigurationStatus.Text = "Select a shared credential before editing it.";
            return;
        }

        var nameBox = new TextBox { Header = "Credential name", Text = selected.Credential.Name };
        var userNameBox = new TextBox { Header = "Username", Text = selected.Credential.UserName };
        var passwordBox = new PasswordBox { Header = "Password (required to replace the protected secret)" };
        TextBlock validationText = CreateDialogValidationText();
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Edit shared credential",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel { Spacing = 12, Children = { validationText, nameBox, userNameBox, passwordBox } }
        };
        KeepDialogOpenWhenInvalid(
            dialog,
            validationText,
            () => ConnectionDialogValidation.GetCredentialError(nameBox.Text, passwordBox.Password),
            () => string.IsNullOrWhiteSpace(nameBox.Text) ? nameBox : passwordBox);
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
            return;

        try
        {
            await _localCredentialStore.SaveAsync(
                selected.Credential with { Name = nameBox.Text, UserName = userNameBox.Text },
                passwordBox.Password);
            await RefreshSharedCredentialsAsync(selected.Credential.Id);
            CredentialConfigurationStatus.Text = "Shared credential updated.";
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or CryptographicException)
        {
            CredentialConfigurationStatus.Text = $"Credential was not updated: {exception.Message}";
        }
    }

    private async void DeleteSharedCredentialButton_Click(object sender, RoutedEventArgs args)
    {
        if (SharedCredentialList.SelectedItem is not CredentialListItem selected)
        {
            CredentialConfigurationStatus.Text = "Select a shared credential before deleting it.";
            return;
        }

        CredentialReference reference = CredentialReference.LocalDpapi(selected.Credential.Id);
        int usageCount = _connectionCatalog.IsLoaded
            ? _connectionCatalog.Tree.Connections.Count(connection => connection.Credential == reference)
            : 0;
        if (usageCount > 0)
        {
            CredentialConfigurationStatus.Text = $"Credential is used by {usageCount} connection(s). Reassign those connections before deleting it.";
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Delete shared credential?",
            Content = $"{selected.Credential.Name} will be deleted for this Windows user.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
            return;

        try
        {
            await _localCredentialStore.DeleteAsync(selected.Credential.Id);
            await RefreshSharedCredentialsAsync();
            CredentialConfigurationStatus.Text = "Shared credential deleted.";
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or InvalidDataException or System.Xml.XmlException or System.Data.Common.DbException)
        {
            CredentialConfigurationStatus.Text = $"Credential was not deleted: {exception.Message}";
        }
    }

    private async Task RefreshSharedCredentialsAsync(Guid? selectedCredentialId = null)
    {
        IReadOnlyList<LocalCredentialDefinition> credentials = await _localCredentialStore.ListAsync();
        List<CredentialListItem> items = credentials
            .OrderBy(credential => credential.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(credential => new CredentialListItem(
                credential,
                string.IsNullOrWhiteSpace(credential.UserName) ? credential.Name : $"{credential.Name} ({credential.UserName})"))
            .ToList();
        SharedCredentialList.ItemsSource = items;
        Guid? id = selectedCredentialId ?? (SharedCredentialList.SelectedItem as CredentialListItem)?.Credential.Id;
        SharedCredentialList.SelectedItem = items.FirstOrDefault(item => item.Credential.Id == id);
    }

    private void RebuildConnectionTree(ConnectionTreeDefinition tree)
    {
        _connectionNodes.Clear();
        ConnectionTree.RootNodes.Clear();
        ConnectionTree.RootNodes.Add(CreateTreeNode(
            LocalTerminalDefinition.CreateSidebarItem(HasOpenLocalTerminalTab())));
        foreach (ConnectionTreeItem item in ConnectionTreeProjection.Create(tree, _connectedConnectionIds))
            ConnectionTree.RootNodes.Add(CreateTreeNode(item));

        RootGrid.DispatcherQueue.TryEnqueue(CompactTreeViewChevronSpacing);
    }

    private void ConnectionTree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        TreeViewNode? node = args.AddedItems.OfType<TreeViewNode>().FirstOrDefault();
        if (node is null)
            return;

        SelectTreeItem(node, openSession: !_suppressSessionOpenForContextMenu);
    }

    private void ConnectionTree_PointerPressed(object sender, PointerRoutedEventArgs args)
    {
        // Selection changes before RightTapped.  Mark the pointer gesture at
        // press time so right-clicking a previously unselected connection does
        // not start or activate a native session behind its context menu.
        if (args.GetCurrentPoint(ConnectionTree).Properties.IsRightButtonPressed)
            _suppressSessionOpenForContextMenu = true;
    }

    private async void ConnectionTree_Tapped(object sender, TappedRoutedEventArgs args)
    {
        if (args.OriginalSource is not DependencyObject source ||
            FindAncestor<ToggleButton>(source) is not null ||
            FindAncestorByName(source, "ExpandCollapseChevron") is not null)
        {
            return;
        }

        TreeViewItem? container = FindAncestor<TreeViewItem>(source);
        TreeViewNode? node = container is null ? null : ConnectionTree.NodeFromContainer(container);
        if (node is null || !_connectionNodes.TryGetValue(node, out ConnectionTreeItem? item))
            return;

        if (item.IsLocalTerminal)
        {
            OpenLocalTerminalSession();
            args.Handled = true;
            return;
        }

        if (!item.IsFolder || !node.HasChildren)
        {
            return;
        }

        node.IsExpanded = !node.IsExpanded;
        if (node.IsExpanded)
            _expandedFolderIds.Add(item.Id);
        else
            _expandedFolderIds.Remove(item.Id);

        await _treeViewStateRepository.SaveExpandedFolderIdsAsync(_expandedFolderIds);
        ConnectionStatus.Text = node.IsExpanded
            ? $"Folder expanded: {item.DisplayName}"
            : $"Folder collapsed: {item.DisplayName}";
    }

    private void SelectTreeItem(TreeViewNode node, bool openSession)
    {
        if (!_connectionNodes.TryGetValue(node, out ConnectionTreeItem? item))
            return;

        _selectedTreeItem = item;
        _selectedConnection = null;
        _selectedFolder = item.IsFolder
            ? _connectionCatalog.Tree.Folders.SingleOrDefault(folder => folder.Id == item.Id)
            : null;
        if (item.IsFolder)
        {
            ConnectionStatus.Text = $"Folder selected: {item.DisplayName}";
            return;
        }
        if (item.IsLocalTerminal)
        {
            ConnectionStatus.Text = "Open a local PowerShell terminal.";
            return;
        }

        // Keep the persisted definition for edit/duplicate commands.  The effective
        // definition is only for the running session; persisting it would flatten
        // folder inheritance back into the connection.
        ConnectionDefinition? persistedDefinition = _connectionCatalog.FindConnection(item.Id);
        ConnectionDefinition? effectiveDefinition = _connectionCatalog.FindResolvedConnection(item.Id);
        if (persistedDefinition is null || effectiveDefinition is null)
            return;

        _selectedConnection = persistedDefinition;
        if (!openSession)
            return;

        RemoteSessionTab tab = _sessionWorkspace.Open(effectiveDefinition);
        TabViewItem? existingTab = _sessionTabs.FirstOrDefault(pair => ReferenceEquals(pair.Value, tab)).Key;
        if (existingTab is null)
        {
            existingTab = CreateSessionTabItem(tab);
            _sessionTabs.Add(existingTab, tab);
            Sessions.TabItems.Add(existingTab);
        }

        bool isAlreadySelected = ReferenceEquals(Sessions.SelectedItem, existingTab);
        Sessions.SelectedItem = existingTab;
        QueueTitleBarInteractiveRegionUpdate();
        if (isAlreadySelected)
            ShowSession(tab);
        if (tab.State is RemoteSessionTabState.Created or RemoteSessionTabState.Faulted)
            _ = ConnectSessionAsync(tab);
    }

    private void OpenLocalTerminalSession()
    {
        ConnectionDefinition definition = LocalTerminalDefinition.CreateSession(_nextLocalTerminalSequence++);
        RemoteSessionTab tab = _sessionWorkspace.Open(definition);
        TabViewItem tabItem = CreateSessionTabItem(tab);
        _sessionTabs.Add(tabItem, tab);
        Sessions.TabItems.Add(tabItem);
        Sessions.SelectedItem = tabItem;
        if (_connectionCatalog.IsLoaded)
            RebuildConnectionTree(_connectionCatalog.Tree);
        QueueTitleBarInteractiveRegionUpdate();
        _ = ConnectSessionAsync(tab);
    }

    private bool HasOpenLocalTerminalTab() =>
        _sessionTabs.Values.Any(tab => LocalTerminalDefinition.IsLocalTerminal(tab.Connection));

    private void ConnectionTree_RightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        try
        {
            if (args.OriginalSource is not DependencyObject source)
                return;

            TreeViewItem? container = FindAncestor<TreeViewItem>(source);
            TreeViewNode? node = container is null ? null : ConnectionTree.NodeFromContainer(container);
            if (container is null || node is null ||
                !_connectionNodes.TryGetValue(node, out ConnectionTreeItem? item))
            {
                return;
            }
            if (item.IsLocalTerminal)
            {
                args.Handled = true;
                return;
            }

            ConnectionTree.SelectedNode = node;
            SelectTreeItem(node, openSession: false);

            MenuFlyout menu = item.IsFolder ? CreateFolderContextMenu() : CreateConnectionContextMenu();
            menu.ShowAt(container, new FlyoutShowOptions { Position = args.GetPosition(container) });
            args.Handled = true;
        }
        finally
        {
            _suppressSessionOpenForContextMenu = false;
        }
    }

    private void ConnectionTree_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args)
    {
        _draggedTreeItem = args.Items.OfType<ConnectionTreeItem>().SingleOrDefault();
        if (_draggedTreeItem?.IsLocalTerminal == true)
        {
            _draggedTreeItem = null;
            args.Cancel = true;
        }
    }

    private void ConnectionTree_DragOver(object sender, DragEventArgs args)
    {
        if (_draggedTreeItem is null)
            return;

        args.AcceptedOperation = DataPackageOperation.Move;
        // Keep the source row stable and use the drop target highlight as the
        // move affordance instead of the TreeView drag proxy.
        args.DragUIOverride.IsCaptionVisible = false;
        args.DragUIOverride.IsContentVisible = false;
        args.DragUIOverride.IsGlyphVisible = false;
    }

    private async void ConnectionTree_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args)
    {
        _draggedTreeItem = null;
        if (args.DropResult != DataPackageOperation.Move)
            return;

        try
        {
            ConnectionTreeDefinition updated = SynchronizeTreeFromView();
            await _connectionCatalog.SaveAsync(updated);
            await LoadConnectionTreeAsync();
            ConnectionStatus.Text = "Selection moved and saved.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException or FormatException)
        {
            ConnectionStatus.Text = $"Selection was not moved: {exception.Message}";
        }
    }

    private ConnectionTreeDefinition SynchronizeTreeFromView()
    {
        ConnectionTreeDefinition current = _connectionCatalog.Tree;
        Guid? rootParentId = current.Folders.SingleOrDefault(folder => folder.IsRoot)?.Id;
        var folderPositions = new Dictionary<Guid, (Guid? ParentFolderId, int SortOrder)>();
        var connectionPositions = new Dictionary<Guid, (Guid? ParentFolderId, int SortOrder)>();
        CollectPositions(ConnectionTree.RootNodes, rootParentId);

        var updated = current with
        {
            Folders = current.Folders.Select(folder => folderPositions.TryGetValue(folder.Id, out var position)
                ? folder with { ParentFolderId = position.ParentFolderId, SortOrder = position.SortOrder }
                : folder).ToArray(),
            Connections = current.Connections.Select(connection => connectionPositions.TryGetValue(connection.Id, out var position)
                ? connection with { ParentFolderId = position.ParentFolderId, SortOrder = position.SortOrder }
                : connection).ToArray()
        };
        updated.Validate();
        return updated;

        void CollectPositions(IList<TreeViewNode> nodes, Guid? parentFolderId)
        {
            int persistedIndex = 0;
            for (int index = 0; index < nodes.Count; index++)
            {
                TreeViewNode node = nodes[index];
                if (!_connectionNodes.TryGetValue(node, out ConnectionTreeItem? item))
                    continue;
                if (item.IsLocalTerminal)
                    continue;

                if (item.IsFolder)
                {
                    folderPositions[item.Id] = (parentFolderId, persistedIndex++);
                    CollectPositions(node.Children, item.Id);
                }
                else
                {
                    connectionPositions[item.Id] = (parentFolderId, persistedIndex++);
                }
            }
        }
    }

    private MenuFlyout CreateConnectionContextMenu()
    {
        var menu = new MenuFlyout();
        menu.Opening += XamlPopup_Opening;
        menu.Opened += XamlPopup_Opened;
        menu.Closed += XamlPopup_Closed;
        menu.Items.Add(CreateContextMenuItem(menu, "Edit connection", Symbol.Edit, EditSelectedNodeButton_Click));
        menu.Items.Add(CreateContextMenuItem(menu, "Copy info", Symbol.Copy, CopySelectedConnectionInfoButton_Click));
        menu.Items.Add(CreateContextMenuItem(menu, "Duplicate", Symbol.Copy, DuplicateSelectedNodeButton_Click));
        menu.Items.Add(CreateContextMenuItem(menu, "Move", Symbol.MoveToFolder, MoveSelectedNodeButton_Click));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateContextMenuItem(menu, "Delete", Symbol.Delete, DeleteSelectedNodeButton_Click));
        return menu;
    }

    private MenuFlyout CreateFolderContextMenu()
    {
        var menu = new MenuFlyout();
        menu.Opening += XamlPopup_Opening;
        menu.Opened += XamlPopup_Opened;
        menu.Closed += XamlPopup_Closed;
        menu.Items.Add(CreateContextMenuItem(menu, "Rename", Symbol.Edit, RenameSelectedFolderButton_Click));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateContextMenuItem(menu, "Delete", Symbol.Delete, DeleteSelectedNodeButton_Click));
        return menu;
    }

    private static MenuFlyoutItem CreateContextMenuItem(MenuFlyout menu, string text, Symbol symbol, RoutedEventHandler click)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Icon = new SymbolIcon(symbol)
        };
        item.Click += async (sender, args) =>
        {
            var menuClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void MenuOnClosed(object? closedSender, object closedArgs) => menuClosed.TrySetResult(true);

            menu.Closed += MenuOnClosed;
            try
            {
                // Flyout item Click fires while its popup is still active. A
                // ContentDialog opened in that interval leaves the flyout's
                // full-window popup behind after dismissal, blocking every
                // later editor. Close and await the actual flyout lifecycle
                // before dispatching any context command.
                menu.Hide();
                await menuClosed.Task;
            }
            finally
            {
                menu.Closed -= MenuOnClosed;
            }

            click(sender, args);
        };
        return item;
    }

    private async void CopySelectedConnectionInfoButton_Click(object sender, RoutedEventArgs args)
    {
        if (_selectedConnection is null)
        {
            ConnectionStatus.Text = "Select a connection before copying its info.";
            return;
        }

        ConnectionDefinition? effectiveDefinition =
            _connectionCatalog.FindResolvedConnection(_selectedConnection.Id);
        if (effectiveDefinition is null)
        {
            ConnectionStatus.Text = "The selected connection no longer exists.";
            return;
        }

        try
        {
            IReadOnlyList<LocalCredentialDefinition> credentials = await _localCredentialStore.ListAsync();
            string clipboardText = ConnectionClipboardInfoFormatter.Format(
                effectiveDefinition,
                credentials,
                _connectionSecretResolver.Resolve(effectiveDefinition, "Password"));
            var content = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            content.SetText(clipboardText);
            Clipboard.SetContent(content);
            ConnectionStatus.Text = "Connection info copied to clipboard.";
        }
        catch (Exception exception)
        {
            ConnectionStatus.Text = $"Connection info was not copied: {exception.Message}";
        }
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T ancestor)
                return ancestor;
        }

        return null;
    }

    private static FrameworkElement? FindAncestorByName(DependencyObject source, string name)
    {
        for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { Name: var currentName } element && currentName == name)
                return element;
        }

        return null;
    }

    private void CompactTreeViewChevronSpacing()
    {
        ConnectionTree.UpdateLayout();
        ApplyCompactChevronPadding(ConnectionTree);
    }

    private static void ApplyCompactChevronPadding(DependencyObject current)
    {
        if (current is Grid { Name: "ExpandCollapseChevron" } chevron)
            chevron.Padding = new Thickness(4, 0, 6, 0);

        int childCount = VisualTreeHelper.GetChildrenCount(current);
        for (int index = 0; index < childCount; index++)
            ApplyCompactChevronPadding(VisualTreeHelper.GetChild(current, index));
    }

    private async void ConnectionTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (_connectionNodes.TryGetValue(args.Node, out ConnectionTreeItem? item) && item.IsFolder)
        {
            _expandedFolderIds.Add(item.Id);
            await _treeViewStateRepository.SaveExpandedFolderIdsAsync(_expandedFolderIds);
            RootGrid.DispatcherQueue.TryEnqueue(CompactTreeViewChevronSpacing);
        }
    }

    private async void ConnectionTree_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
    {
        if (_connectionNodes.TryGetValue(args.Node, out ConnectionTreeItem? item) && item.IsFolder)
        {
            _expandedFolderIds.Remove(item.Id);
            await _treeViewStateRepository.SaveExpandedFolderIdsAsync(_expandedFolderIds);
        }
    }

    private async void DuplicateSelectedNodeButton_Click(object sender, RoutedEventArgs args)
    {
        if (!CanEditConnectionStore())
            return;

        if (_selectedConnection is null && _selectedFolder is null)
        {
            ConnectionStatus.Text = "Select a folder or connection before duplicating it.";
            return;
        }

        try
        {
            ConnectionTreeDefinition updated = _selectedConnection is { } connection
                ? ConnectionTreeEditor.DuplicateConnection(_connectionCatalog.Tree, connection.Id, CloneConnectionOptions)
                : ConnectionTreeEditor.DuplicateFolder(_connectionCatalog.Tree, _selectedFolder!.Id, CloneConnectionOptions);
            await _connectionCatalog.SaveAsync(updated);
            await LoadConnectionTreeAsync();
            ConnectionStatus.Text = _selectedConnection is not null ? "Connection duplicated." : "Folder subtree duplicated.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException or FormatException)
        {
            ConnectionStatus.Text = $"Selection was not duplicated: {exception.Message}";
        }
    }

    private ConnectionNodeOptions? CloneConnectionOptions(ConnectionDefinition source, Guid targetConnectionId) =>
        _connectionOptionsEditor.ReprotectSecrets(source, targetConnectionId);

    private async void DeleteSelectedNodeButton_Click(object sender, RoutedEventArgs args)
    {
        if (!CanEditConnectionStore())
            return;

        if (_selectedTreeItem is null)
        {
            ConnectionStatus.Text = "Select a folder or connection before deleting it.";
            return;
        }

        var confirmation = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = _selectedTreeItem.IsFolder ? "Delete folder?" : "Delete connection?",
            Content = _selectedTreeItem.IsFolder
                ? "The folder, its child folders, and every contained connection will be deleted."
                : "The selected connection will be deleted.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await ShowDialogAsync(confirmation) != ContentDialogResult.Primary)
            return;

        try
        {
            ConnectionTreeDefinition current = _connectionCatalog.Tree;
            ConnectionTreeDefinition updated = _selectedTreeItem.IsFolder
                ? ConnectionTreeEditor.DeleteFolder(current, _selectedTreeItem.Id)
                : ConnectionTreeEditor.DeleteConnection(current, _selectedTreeItem.Id);
            await _connectionCatalog.SaveAsync(updated);
            var retainedConnectionIds = updated.Connections.Select(connection => connection.Id).ToHashSet();
            foreach (Guid connectionId in current.Connections
                         .Where(connection => !retainedConnectionIds.Contains(connection.Id))
                         .Select(connection => connection.Id))
            {
                await CloseSessionTabsForConnectionAsync(connectionId);
            }
            _selectedTreeItem = null;
            _selectedConnection = null;
            _selectedFolder = null;
            await LoadConnectionTreeAsync();
            ConnectionStatus.Text = "Selection deleted.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException or FormatException)
        {
            ConnectionStatus.Text = $"Selection was not deleted: {exception.Message}";
        }
    }

    private void TryInstallSessionHotKeyController()
    {
        try
        {
            _sessionHotKeyController = new WindowSessionHotKeyController(
                WindowNative.GetWindowHandle(this),
                QueueSessionTabNavigation,
                EmbeddingDiagnostics.Write,
                RecoverSessionKeyboardFocus);
            _sessionHotKeyController.SetEnabled(true);
        }
        catch (Exception exception)
        {
            _sessionHotKeyController?.Dispose();
            _sessionHotKeyController = null;
            EmbeddingDiagnostics.Write(
                $"session-keyboard-hook-failed type={exception.GetType().Name} hresult={exception.HResult}");
        }
    }

    private void QueueSessionTabNavigation(int direction)
    {
        if (direction is not (-1 or 1))
            return;

        _ = Interlocked.Add(ref _pendingSessionTabNavigation, direction);
        QueuePendingSessionTabNavigation();
    }

    private void QueuePendingSessionTabNavigation()
    {
        if (Interlocked.CompareExchange(ref _sessionTabNavigationQueued, 1, 0) != 0)
            return;

        if (DispatcherQueue.TryEnqueue(ProcessQueuedSessionTabNavigation))
            return;

        _ = Interlocked.Exchange(ref _sessionTabNavigationQueued, 0);
    }

    private void ProcessQueuedSessionTabNavigation()
    {
        int offset = Interlocked.Exchange(ref _pendingSessionTabNavigation, 0);
        try
        {
            if (offset != 0)
            {
                NavigateSessionTabs(offset);
                // This method always runs on the WinUI dispatcher. Restore
                // focus only after SelectionChanged has attached the newly
                // selected native session; doing it in the low-level keyboard
                // hook races the tab change and uses the wrong input queue.
                _embeddedSessionSurface?.RestoreFocusAfterTransition();
            }
        }
        finally
        {
            _ = Interlocked.Exchange(ref _sessionTabNavigationQueued, 0);
            if (Volatile.Read(ref _pendingSessionTabNavigation) != 0)
                QueuePendingSessionTabNavigation();
        }
    }

    private void RecoverSessionKeyboardFocus()
    {
        // This callback can originate from the Win32 low-level keyboard hook.
        // Never touch WinUI controls or cross-process focus from that thread.
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ProcessQueuedSessionTabNavigation();
            _embeddedSessionSurface?.RestoreFocusAfterTransition();
        });
    }

    private void NavigateSessionTabs(int direction)
    {
        if (Sessions.SelectedItem is not TabViewItem currentTab)
            return;

        TabViewItem[] tabs = Sessions.TabItems
            .OfType<TabViewItem>()
            .Where(_sessionTabs.ContainsKey)
            .ToArray();
        TabViewItem? selectedTab = SessionTabSelection.SelectRelative(tabs, currentTab, direction);
        if (selectedTab is not null)
            Sessions.SelectedItem = selectedTab;
    }

    private void TryInstallSessionPointerController()
    {
        try
        {
            _sessionPointerController = new WindowSessionPointerController(
                WindowNative.GetWindowHandle(this),
                TryQueueCloseSessionTabAtClientPoint,
                GetEmbeddedSessionFocusTargetHandle,
                QueueEmbeddedSessionFocus,
                CancelEmbeddedSessionFocusRestore);
            EmbeddingDiagnostics.Write("session-pointer-hook-installed");
        }
        catch (Exception exception)
        {
            _sessionPointerController?.Dispose();
            _sessionPointerController = null;
            EmbeddingDiagnostics.Write(
                $"session-pointer-hook-failed type={exception.GetType().Name} hresult={exception.HResult}");
        }
    }

    private IntPtr GetEmbeddedSessionFocusTargetHandle() =>
        _embeddedSessionSurface?.FocusTargetWindowHandle ?? IntPtr.Zero;

    private void QueueEmbeddedSessionFocus()
    {
        Volatile.Write(ref _embeddedFocusRestoreSuppressedUntil, 0);
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (!_windowMinimized)
                _embeddedSessionSurface?.Focus();
        });
    }

    private void CancelEmbeddedSessionFocusRestore()
    {
        Volatile.Write(
            ref _embeddedFocusRestoreSuppressedUntil,
            SessionFocusRestorePolicy.CreateShellClickSuppressionDeadline(
                Environment.TickCount64));
        _embeddedSessionSurface?.CancelPendingFocusRestore();
    }

    private bool IsEmbeddedFocusRestoreSuppressed() =>
        SessionFocusRestorePolicy.ShouldSuppressForShellClick(
            Volatile.Read(ref _embeddedFocusRestoreSuppressedUntil),
            Environment.TickCount64);

    private bool TryQueueCloseSessionTabAtClientPoint(int physicalX, int physicalY)
    {
        if (RootGrid.XamlRoot is null || RootGrid.XamlRoot.RasterizationScale <= 0)
            return false;

        double scale = RootGrid.XamlRoot.RasterizationScale;
        double logicalX = physicalX / scale;
        double logicalY = physicalY / scale;
        foreach (TabViewItem tab in Sessions.TabItems.OfType<TabViewItem>())
        {
            if (!_sessionTabs.ContainsKey(tab) || !tab.IsLoaded)
                continue;

            Windows.Foundation.Point origin = tab.TransformToVisual(RootGrid)
                .TransformPoint(new Windows.Foundation.Point(0, 0));
            if (!SessionTabPointerPolicy.ContainsPoint(
                    logicalX,
                    logicalY,
                    origin.X,
                    origin.Y,
                    tab.ActualWidth,
                    tab.ActualHeight))
            {
                continue;
            }

            EmbeddingDiagnostics.Write(
                $"session-tab-middle-click x={physicalX} y={physicalY} scale={scale:F2}");
            return DispatcherQueue.TryEnqueue(() => _ = CloseSessionTabAsync(Sessions, tab));
        }

        EmbeddingDiagnostics.Write(
            $"session-tab-middle-click-miss x={physicalX} y={physicalY} scale={scale:F2}");
        return false;
    }

    private async void RenameSelectedFolderButton_Click(object sender, RoutedEventArgs args)
    {
        if (!CanEditConnectionStore())
            return;

        if (_selectedFolder is null)
        {
            ConnectionStatus.Text = "Select a folder before renaming it.";
            return;
        }

        ConnectionFolderDefinition folder = _selectedFolder;
        var nameBox = new TextBox { Header = "Folder name", Text = folder.Name };
        nameBox.Loaded += (_, _) =>
        {
            nameBox.Focus(FocusState.Programmatic);
            nameBox.SelectAll();
        };
        TextBlock validationText = CreateDialogValidationText();
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Rename folder",
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel
            {
                Spacing = 12,
                Children = { validationText, nameBox }
            }
        };
        KeepDialogOpenWhenInvalid(
            dialog,
            validationText,
            () => ConnectionDialogValidation.GetFolderError(nameBox.Text),
            () => nameBox);
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
            return;

        try
        {
            await _connectionCatalog.SaveAsync(ConnectionTreeEditor.RenameFolder(
                _connectionCatalog.Tree,
                folder.Id,
                nameBox.Text));
            _selectedFolder = null;
            await LoadConnectionTreeAsync();
            ConnectionStatus.Text = "Folder renamed.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException or FormatException)
        {
            ConnectionStatus.Text = $"Folder was not renamed: {exception.Message}";
        }
    }

    private async void MoveSelectedNodeButton_Click(object sender, RoutedEventArgs args)
    {
        if (!CanEditConnectionStore())
            return;

        if (_selectedTreeItem is null)
        {
            ConnectionStatus.Text = "Select a folder or connection before moving it.";
            return;
        }

        var destinations = new List<FolderDestination> { new(null, "Root") };
        destinations.AddRange(_connectionCatalog.Tree.Folders
            .Where(folder => folder.Id != _selectedTreeItem.Id)
            .OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(folder => new FolderDestination(folder.Id, folder.Name)));
        var destinationBox = new ComboBox
        {
            Header = "Destination",
            ItemsSource = destinations,
            DisplayMemberPath = nameof(FolderDestination.Name),
            SelectedIndex = 0
        };
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Move selection",
            Content = destinationBox,
            PrimaryButtonText = "Move",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary || destinationBox.SelectedItem is not FolderDestination destination)
            return;

        try
        {
            ConnectionTreeDefinition updated = _selectedTreeItem.IsFolder
                ? ConnectionTreeEditor.MoveFolder(_connectionCatalog.Tree, _selectedTreeItem.Id, destination.Id)
                : ConnectionTreeEditor.MoveConnection(_connectionCatalog.Tree, _selectedTreeItem.Id, destination.Id);
            await _connectionCatalog.SaveAsync(updated);
            await LoadConnectionTreeAsync();
            ConnectionStatus.Text = "Selection moved.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException or FormatException)
        {
            ConnectionStatus.Text = $"Selection was not moved: {exception.Message}";
        }
    }

    private async void Sessions_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab is TabViewItem tab)
            await CloseSessionTabAsync(sender, tab);
    }

    private async void SessionTab_PointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (sender is not TabViewItem tab)
            return;

        var properties = args.GetCurrentPoint(tab).Properties;
        bool middleButtonPressed = properties.IsMiddleButtonPressed ||
            properties.PointerUpdateKind is Microsoft.UI.Input.PointerUpdateKind.MiddleButtonPressed or
                Microsoft.UI.Input.PointerUpdateKind.MiddleButtonReleased;
        EmbeddingDiagnostics.Write(
            $"session-tab-pointer event={args.Pointer.PointerDeviceType} update={properties.PointerUpdateKind} middle={properties.IsMiddleButtonPressed} closable={tab.IsClosable}");
        if (!SessionTabPointerPolicy.ShouldClose(middleButtonPressed, tab.IsClosable))
            return;

        args.Handled = true;
        await CloseSessionTabAsync(Sessions, tab);
    }

    private TabViewItem CreateSessionTabItem(RemoteSessionTab sessionTab)
    {
        ArgumentNullException.ThrowIfNull(sessionTab);
        var tab = new TabViewItem { Header = new SessionTabHeader(sessionTab), IsClosable = true };
        // TabView consumes pointer input internally. Listen to already-handled
        // routed events so middle-click remains a first-class close gesture.
        tab.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler(SessionTab_PointerPressed),
            handledEventsToo: true);
        tab.AddHandler(
            UIElement.PointerReleasedEvent,
            new PointerEventHandler(SessionTab_PointerPressed),
            handledEventsToo: true);
        return tab;
    }

    private async Task CloseSessionTabAsync(TabView sender, TabViewItem tab)
    {
        if (!_sessionTabCloseGate.TryEnter())
            return;

        try
        {
            if (!_sessionTabs.Remove(tab, out RemoteSessionTab? sessionTab))
                return;
            _subscribedResourceMonitorTabs.Remove(sessionTab);

            bool closingSelectedTab = ReferenceEquals(sender.SelectedItem, tab);
            TabViewItem[] sessionTabsBeforeClose = sender.TabItems
                .OfType<TabViewItem>()
                .Where(candidate => _sessionTabs.ContainsKey(candidate) || ReferenceEquals(candidate, tab))
                .ToArray();
            TabViewItem? tabToActivate = closingSelectedTab
                ? SessionTabSelection.SelectAfterClose(sessionTabsBeforeClose, tab)
                : null;

            // Remove the tab before closing the native protocol process. PuTTY and
            // the RDP ActiveX control can take a moment to stop; keeping the tab
            // visible until then makes the first close click look ignored.
            if (closingSelectedTab)
                RemoteSessionWorkspace.Deactivate(_embeddedSessionSurface);
            sender.TabItems.Remove(tab);
            if (tabToActivate is not null)
                sender.SelectedItem = tabToActivate;
            QueueTitleBarInteractiveRegionUpdate();
            await Task.Yield();

            await _sessionWorkspace.CloseAsync(sessionTab);
            _pendingRestoredConnectionIds.Remove(sessionTab.Connection.Id);
            _connectedConnectionIds.Remove(sessionTab.Connection.Id);
            if (_connectionCatalog.IsLoaded)
                RebuildConnectionTree(_connectionCatalog.Tree);

            if (sender.SelectedItem is not TabViewItem selected || !_sessionTabs.ContainsKey(selected))
                ConnectionsNavigationButton_Click(this, new RoutedEventArgs());
        }
        catch (Exception exception)
        {
            ConnectionStatus.Text = $"Session closed with an error: {exception.Message}";
        }
        finally
        {
            _sessionTabCloseGate.Exit();
        }
    }

    private async void CloseAllSessionsButton_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            await _sessionWorkspace.CloseAllAsync();
            foreach (TabViewItem tab in _sessionTabs.Keys.ToArray())
                Sessions.TabItems.Remove(tab);
            _sessionTabs.Clear();
            _subscribedResourceMonitorTabs.Clear();
            _pendingRestoredConnectionIds.Clear();
            QueueTitleBarInteractiveRegionUpdate();
            _connectedConnectionIds.Clear();
            if (_connectionCatalog.IsLoaded)
                RebuildConnectionTree(_connectionCatalog.Tree);
            ConnectionsNavigationButton_Click(this, new RoutedEventArgs());
            ConnectionStatus.Text = "All sessions closed.";
        }
        catch (Exception exception)
        {
            ConnectionStatus.Text = $"One or more sessions did not close cleanly: {exception.Message}";
        }
    }

    private void Sessions_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        UpdateSessionTabHeaderSelection();
        foreach (var recoveringTab in _sessionTabs.Values.Where(tab => tab.IsReconnecting && !IsSelectedSession(tab)))
            recoveringTab.CancelPendingConnection();

        if (Sessions.SelectedItem is TabViewItem tab && _sessionTabs.TryGetValue(tab, out RemoteSessionTab? sessionTab))
        {
            ShowSession(
                sessionTab,
                SessionPresentationPolicy.ShouldActivateNativeSession(SessionPresentationTrigger.TabSelection));
            TryConnectRestoredSession(sessionTab);
        }

        QueueTitleBarInteractiveRegionUpdate();
    }

    private async void ConnectSessionButton_Click(object sender, RoutedEventArgs args)
    {
        if (Sessions.SelectedItem is not TabViewItem tab ||
            !_sessionTabs.TryGetValue(tab, out RemoteSessionTab? sessionTab))
        {
            return;
        }

        await ConnectSessionAsync(sessionTab);
    }

    private async Task ConnectSessionAsync(RemoteSessionTab sessionTab, bool propagateFailure = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_embeddedSessionSurface is null ||
            sessionTab.State is RemoteSessionTabState.Connecting or RemoteSessionTabState.Connected)
        {
            return;
        }

        ConnectSessionButton.IsEnabled = false;
        SessionStatus.Text = propagateFailure ? "Reconnecting SSH..." : "Connecting...";
        RemoteSessionWorkspace.Deactivate(_embeddedSessionSurface);
        SessionContent.Visibility = Visibility.Collapsed;
        SessionLoadingContent.Visibility = Visibility.Visible;
        try
        {
            // RDP ActiveX creation must stay on this STA thread, but it can
            // synchronously create an ATL child window. Yield once after the
            // loading state is visible so the pointer-up and queued mouse
            // messages are processed before that native work begins.
            if (sessionTab.Connection.Protocol == ProtocolKind.Rdp)
                await Task.Yield();

            await _sessionWorkspace.ConnectAsync(sessionTab, _embeddedSessionSurface, cancellationToken);
            SubscribeResourceMonitor(sessionTab);
            _connectedConnectionIds.Add(sessionTab.Connection.Id);
            if (_connectionCatalog.IsLoaded)
                RebuildConnectionTree(_connectionCatalog.Tree);
            PresentSelectedSessionAfterConnectionCompleted(sessionTab);
        }
        catch (Exception exception)
        {
            EmbeddingDiagnostics.Write(
                $"session-connect-failed type={exception.GetType().Name} hresult=0x{exception.HResult:X8} " +
                $"stack={exception.StackTrace?.Replace(Environment.NewLine, " | ")}");
            if (IsSelectedSession(sessionTab))
            {
                ShowSession(sessionTab);
                SessionStatus.Text = SessionPresentationPolicy.FormatConnectionFailure(exception.Message);
            }
            if (propagateFailure)
                throw;
        }
        finally
        {
            if (IsSelectedSession(sessionTab))
            {
                if (sessionTab.State is not RemoteSessionTabState.Connecting)
                    SessionLoadingContent.Visibility = Visibility.Collapsed;
                ConnectSessionButton.IsEnabled = sessionTab.State is not RemoteSessionTabState.Connected;
            }
        }
    }

    private void PresentSelectedSessionAfterConnectionCompleted(RemoteSessionTab completedSession)
    {
        if (Sessions.SelectedItem is not TabViewItem selectedTab ||
            !_sessionTabs.TryGetValue(selectedTab, out RemoteSessionTab? selectedSession))
        {
            return;
        }

        bool restoreSelectedSession = SessionPresentationPolicy.ShouldRestoreSelectedSession(
            completedSession,
            selectedSession);
        bool activateNativeSession = SessionPresentationPolicy.ShouldActivateNativeSession(
            restoreSelectedSession
                ? SessionPresentationTrigger.TabSelection
                : SessionPresentationTrigger.ConnectionCompleted);
        ShowSession(selectedSession, activateNativeSession);
    }

    private void UpdateSessionTabHeaderSelection()
    {
        foreach (TabViewItem tab in _sessionTabs.Keys)
        {
            if (tab.Header is SessionTabHeader header)
                header.UpdateSelection(ReferenceEquals(Sessions.SelectedItem, tab));
        }
    }

    private void ShowSession(RemoteSessionTab tab, bool activateNativeSession = true)
    {
        SetResourceMonitoringActive(tab);
        WelcomeContent.Visibility = Visibility.Collapsed;
        ConfigContent.Visibility = Visibility.Collapsed;
        SessionLoadingContent.Visibility = Visibility.Collapsed;
        SessionTitle.Text = tab.Connection.Name;
        SessionStatus.Text = tab.State switch
        {
            RemoteSessionTabState.Created => "Ready to connect.",
            RemoteSessionTabState.Connected => "Connected",
            RemoteSessionTabState.Connecting => "Connecting...",
            RemoteSessionTabState.Faulted => "Connection failed.",
            RemoteSessionTabState.Closed => "Closed",
            _ => string.Empty
        };
        if (_embeddedSessionSurface is not null &&
            SessionPresentationPolicy.ShouldDeactivateNativeSurface(tab.State))
        {
            RemoteSessionWorkspace.Deactivate(_embeddedSessionSurface);
        }
        if (tab.State == RemoteSessionTabState.Connecting)
        {
            UpdateResourceMonitor(tab);
            SessionContent.Visibility = Visibility.Collapsed;
            SessionLoadingContent.Visibility = Visibility.Visible;
            return;
        }
        if (tab.Session is IWinUIContentSession managedSession && tab.State == RemoteSessionTabState.Connected)
        {
            RemoteSessionWorkspace.Deactivate(_embeddedSessionSurface);
            ManagedSessionContent.Content = managedSession.View;
            ManagedSessionContent.Visibility = Visibility.Visible;
            SessionContent.Visibility = Visibility.Collapsed;
            managedSession.Activate();
            UpdateResourceMonitor(tab);
            return;
        }

        HideManagedSession();
        if (_embeddedSessionSurface is not null && tab.State == RemoteSessionTabState.Connected)
        {
            SessionContent.Visibility = Visibility.Collapsed;
            ConnectSessionButton.IsEnabled = false;
            if (!activateNativeSession)
            {
                UpdateResourceMonitor(tab);
                return;
            }

            try
            {
                RemoteSessionWorkspace.Activate(tab, _embeddedSessionSurface);
                UpdateResourceMonitor(tab);
            }
            catch (Exception exception)
            {
                SessionStatus.Text = $"Could not activate session: {exception.Message}";
            }
            return;
        }

        SessionContent.Visibility = Visibility.Visible;
        ConnectSessionButton.IsEnabled = tab.State is RemoteSessionTabState.Created or RemoteSessionTabState.Faulted;
        UpdateResourceMonitor(tab);
    }

    private void SubscribeResourceMonitor(RemoteSessionTab tab)
    {
        IRemoteResourceMonitor? monitor = tab.ResourceMonitor;
        if (monitor is null ||
            _subscribedResourceMonitorTabs.TryGetValue(tab, out var subscribed) && ReferenceEquals(subscribed, monitor))
            return;

        _subscribedResourceMonitorTabs[tab] = monitor;

        monitor.SnapshotUpdated += _ => QueueResourceMonitorUpdate(tab);
        monitor.StatusChanged += _ => QueueResourceMonitorUpdate(tab);
    }

    private void QueueResourceMonitorUpdate(RemoteSessionTab tab)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (IsSelectedSession(tab))
                UpdateResourceMonitor(tab);
        });
    }

    private bool IsSelectedSession(RemoteSessionTab tab) =>
        Sessions.SelectedItem is TabViewItem selectedTab &&
        _sessionTabs.TryGetValue(selectedTab, out RemoteSessionTab? selectedSession) &&
        ReferenceEquals(selectedSession, tab);

    private void SetResourceMonitoringActive(RemoteSessionTab? activeTab)
    {
        foreach (RemoteSessionTab candidate in _sessionTabs.Values)
            candidate.SetResourceMonitoringActive(ReferenceEquals(candidate, activeTab));
    }

    private void UpdateResourceMonitor(RemoteSessionTab tab)
    {
        IRemoteResourceMonitor? monitor = tab.ResourceMonitor;
        if (tab.Connection.Protocol is not (ProtocolKind.Ssh2 or ProtocolKind.Rdp) ||
            tab.State != RemoteSessionTabState.Connected ||
            monitor is null)
        {
            CloseResourceMonitorDetailToolTips();
            ResourceMonitorBar.Visibility = Visibility.Collapsed;
            return;
        }

        ResourceMonitorBar.Visibility = Visibility.Visible;
        RemoteResourceSnapshot? snapshot = monitor.LastSnapshot;
        ResourceMonitorCpu.Text = snapshot?.CpuPercent is double cpu ? $"{cpu:0} %" : "--";
        ResourceMonitorMemory.Text = ResourceMonitorPresentation.FormatMemoryPercent(snapshot);
        ResourceMonitorDisk.Text = ResourceMonitorPresentation.FormatDiskPercent(snapshot);
        ResourceMonitorReceive.Text = snapshot?.ReceiveBytesPerSecond is long receive ? $"{FormatBytes(receive)}/s" : "--";
        ResourceMonitorTransmit.Text = snapshot?.TransmitBytesPerSecond is long transmit ? $"{FormatBytes(transmit)}/s" : "--";
        ResourceMonitorUptime.Text = snapshot is null ? "--" : FormatUptime(snapshot.Uptime);

        string memoryDetails = snapshot is null
            ? monitor.LastStatus.Message
            : ResourceMonitorPresentation.FormatMemoryDetails(snapshot);
        string diskDetails = snapshot is null
            ? monitor.LastStatus.Message
            : ResourceMonitorPresentation.FormatDiskDetails(snapshot);
        ResourceMonitorMemoryDetails.Text = memoryDetails;
        ResourceMonitorDiskDetails.Text = diskDetails;

        ApplyResourceWarning(ResourceMonitorCpuChip, ResourceMonitorCpuLabel, ResourceMonitorCpu, snapshot?.CpuPercent);
        ApplyResourceWarning(ResourceMonitorMemoryChip, ResourceMonitorMemoryLabel, ResourceMonitorMemory, ResourceMonitorPresentation.GetMemoryPercent(snapshot));
        ApplyResourceWarning(ResourceMonitorDiskChip, ResourceMonitorDiskLabel, ResourceMonitorDisk, snapshot?.DiskPercent);
        UpdateSshWorkingDirectory(tab);
    }

    private void ResourceMonitorDetailChip_PointerEntered(object sender, PointerRoutedEventArgs args)
    {
        if (sender is not FrameworkElement chip || GetResourceMonitorDetailToolTip(chip) is not ToolTip toolTip)
            return;

        _hoveredResourceMonitorDetailChips.Add(chip);
        toolTip.IsOpen = true;
    }

    private void ResourceMonitorDetailChip_PointerExited(object sender, PointerRoutedEventArgs args)
    {
        if (sender is not FrameworkElement chip || GetResourceMonitorDetailToolTip(chip) is not ToolTip toolTip)
            return;

        _hoveredResourceMonitorDetailChips.Remove(chip);
        toolTip.IsOpen = false;
    }

    private void ResourceMonitorDetailToolTip_Closed(object sender, object args)
    {
        if (sender is not ToolTip toolTip || GetResourceMonitorDetailChip(toolTip) is not FrameworkElement chip ||
            !_hoveredResourceMonitorDetailChips.Contains(chip))
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (_hoveredResourceMonitorDetailChips.Contains(chip))
                toolTip.IsOpen = true;
        });
    }

    private ToolTip? GetResourceMonitorDetailToolTip(FrameworkElement chip)
    {
        if (ReferenceEquals(chip, ResourceMonitorMemoryChip))
            return ResourceMonitorMemoryToolTip;
        if (ReferenceEquals(chip, ResourceMonitorDiskChip))
            return ResourceMonitorDiskToolTip;
        return null;
    }

    private Border? GetResourceMonitorDetailChip(ToolTip toolTip)
    {
        if (ReferenceEquals(toolTip, ResourceMonitorMemoryToolTip))
            return ResourceMonitorMemoryChip;
        if (ReferenceEquals(toolTip, ResourceMonitorDiskToolTip))
            return ResourceMonitorDiskChip;
        return null;
    }

    private void CloseResourceMonitorDetailToolTips()
    {
        _hoveredResourceMonitorDetailChips.Clear();
        ResourceMonitorMemoryToolTip.IsOpen = false;
        ResourceMonitorDiskToolTip.IsOpen = false;
    }

    private async void SshWorkingDirectoryTimerOnTick(object? sender, object args)
    {
        if (Sessions.SelectedItem is TabViewItem selectedTab &&
            _sessionTabs.TryGetValue(selectedTab, out RemoteSessionTab? sessionTab))
        {
            UpdateSshWorkingDirectory(sessionTab);
            if (!sessionTab.IsReconnecting &&
                sessionTab.Session is PuttyProtocolSession putty &&
                putty.TryConsumeNetworkReconnectRequest())
            {
                await ReconnectSshSessionAsync(sessionTab);
            }
        }
    }

    private async Task ReconnectSshSessionAsync(RemoteSessionTab tab)
    {
        using var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        tab.BeginReconnect(cancellation);
        try
        {
            if (IsSelectedSession(tab))
            {
                RemoteSessionWorkspace.Deactivate(_embeddedSessionSurface);
                SessionContent.Visibility = Visibility.Collapsed;
                SessionLoadingContent.Visibility = Visibility.Visible;
                SessionStatus.Text = "Closing disconnected SSH session...";
                ConnectSessionButton.IsEnabled = false;
            }
            _connectedConnectionIds.Remove(tab.Connection.Id);
            await _sessionWorkspace.PrepareReconnectAsync(tab, cancellationToken);
            await SshReconnectRetry.RunAsync(async token =>
            {
                if (_disposed || !IsSelectedSession(tab) || !_sessionTabs.ContainsValue(tab))
                    throw new OperationCanceledException(token);
                try
                {
                    await ConnectSessionAsync(tab, propagateFailure: true, cancellationToken: token);
                }
                finally
                {
                    if (IsSelectedSession(tab))
                        ConnectSessionButton.IsEnabled = false;
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Closing the tab/window or selecting another tab cancels recovery.
        }
        catch (Exception exception)
        {
            if (IsSelectedSession(tab))
                SessionStatus.Text = $"SSH reconnect failed: {exception.Message} Click Connect to retry.";
        }
        finally
        {
            tab.EndReconnect();
            if (IsSelectedSession(tab))
            {
                SessionLoadingContent.Visibility = Visibility.Collapsed;
                SessionContent.Visibility = tab.State == RemoteSessionTabState.Connected ? Visibility.Collapsed : Visibility.Visible;
                ConnectSessionButton.IsEnabled = tab.State is not RemoteSessionTabState.Connected and not RemoteSessionTabState.Connecting;
            }
        }
    }

    private void UpdateSshWorkingDirectory(RemoteSessionTab tab)
    {
        bool isConnectedSsh = tab.Connection.Protocol == ProtocolKind.Ssh2 &&
                              !LocalTerminalDefinition.IsLocalTerminal(tab.Connection) &&
                              tab.State == RemoteSessionTabState.Connected;
        SshWorkingDirectoryButton.Visibility = isConnectedSsh ? Visibility.Visible : Visibility.Collapsed;
        if (!isConnectedSsh)
            return;

        string? workingDirectory = (tab.Session as IRemoteWorkingDirectorySession)?.CurrentWorkingDirectory;
        SshWorkingDirectoryText.Text = workingDirectory ?? "Unavailable";
        ToolTipService.SetToolTip(
            SshWorkingDirectoryButton,
            workingDirectory is null
                ? "Open SFTP in the SSH home directory. The shell has not reported its current directory."
                : $"Open SFTP at {workingDirectory}");
    }

    private async void SshWorkingDirectoryButton_Click(object sender, RoutedEventArgs args)
    {
        if (_sftpDialogOpen ||
            Sessions.SelectedItem is not TabViewItem selectedTab ||
            !_sessionTabs.TryGetValue(selectedTab, out RemoteSessionTab? sessionTab) ||
            sessionTab.Connection.Protocol != ProtocolKind.Ssh2 ||
            sessionTab.State != RemoteSessionTabState.Connected)
        {
            return;
        }

        _sftpDialogOpen = true;
        try
        {
            string? workingDirectory = (sessionTab.Session as IRemoteWorkingDirectorySession)?.CurrentWorkingDirectory;
            var browser = new SftpBrowserDialog(this, _sshFileTransferSessionFactory);
            await browser.ShowAsync(sessionTab.Connection, workingDirectory);
        }
        finally
        {
            _sftpDialogOpen = false;
            RecoverSessionKeyboardFocus();
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(bytes, 0);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }

    private static void ApplyResourceWarning(
        Border chip,
        TextBlock label,
        TextBlock value,
        double? percent)
    {
        bool warning = ResourceMonitorPresentation.IsWarning(percent);
        chip.Background = warning ? ResourceWarningBackground : null;
        if (warning)
        {
            label.Foreground = ResourceWarningForeground;
            value.Foreground = ResourceWarningForeground;
        }
        else
        {
            label.ClearValue(TextBlock.ForegroundProperty);
            value.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    private static void ApplyResourceWarning(Control chip, double? percent)
    {
        bool warning = ResourceMonitorPresentation.IsWarning(percent);
        chip.Background = warning ? ResourceWarningBackground : TransparentBrush;
        if (warning)
            chip.Foreground = ResourceWarningForeground;
        else
            chip.ClearValue(Control.ForegroundProperty);
    }

    private static string FormatUptime(TimeSpan uptime) => uptime.TotalDays >= 1
        ? $"{uptime.Days}d {uptime.Hours}h"
        : $"{uptime.Hours}h {uptime.Minutes}m";

    private void HideManagedSession()
    {
        ManagedSessionContent.Content = null;
        ManagedSessionContent.Visibility = Visibility.Collapsed;
    }

    private async Task CloseSessionTabsForConnectionAsync(Guid connectionId)
    {
        foreach ((TabViewItem tab, RemoteSessionTab session) in _sessionTabs
                     .Where(pair => pair.Value.Connection.Id == connectionId)
                     .Select(pair => (pair.Key, pair.Value))
                     .ToArray())
        {
            await _sessionWorkspace.CloseAsync(session);
            _pendingRestoredConnectionIds.Remove(session.Connection.Id);
            _sessionTabs.Remove(tab);
            Sessions.TabItems.Remove(tab);
            QueueTitleBarInteractiveRegionUpdate();
        }
    }
}

internal sealed record FolderDestination(Guid? Id, string Name);
internal sealed record CredentialChoice(LoipvRemote.Domain.Credentials.CredentialReference Reference, string Name, string UserName = "");
internal sealed record CredentialListItem(LocalCredentialDefinition Credential, string DisplayName);
