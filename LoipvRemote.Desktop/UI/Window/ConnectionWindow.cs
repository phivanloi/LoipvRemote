using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using LoipvRemote.App;
using LoipvRemote.App.Info;
using LoipvRemote.Config;
using LoipvRemote.Connection;
using LoipvRemote.UI.Adapters;
using LoipvRemote.Messages;
using LoipvRemote.Properties;
using LoipvRemote.Themes;
using LoipvRemote.Tools;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Tabs;
using LoipvRemote.UI.TaskDialog;
using WeifenLuo.WinFormsUI.Docking;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;
using LoipvRemote.Security;
using LoipvRemote.UseCases.Navigation;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.UI.Window
{
    [SupportedOSPlatform("windows")]
    public partial class ConnectionWindow : BaseWindow
    {
        private VisualStudioToolStripExtender _vsToolStripExtender = null!;
        private readonly ToolStripRenderer _toolStripProfessionalRenderer = new ToolStripProfessionalRenderer();
        private MessageCollector? _messageCollector;
        private ExternalToolsService? _externalToolsService;
        private IConnectionInitiator? _connectionInitiator;
        private ConnectionWorkspaceAdapter? _connectionWorkspace;
        private DesktopWindowCatalog? _windows;

        private MessageCollector MessageCollector => _messageCollector
            ?? throw new InvalidOperationException("ConnectionWindow services must be attached before use.");

        private ExternalToolsService ExternalToolsService => _externalToolsService
            ?? throw new InvalidOperationException("ConnectionWindow services must be attached before use.");

        private IConnectionInitiator ConnectionInitiator => _connectionInitiator
            ?? throw new InvalidOperationException("ConnectionWindow services must be attached before use.");

        private ConnectionWorkspaceAdapter ConnectionWorkspace => _connectionWorkspace
            ?? throw new InvalidOperationException("ConnectionWindow services must be attached before use.");

        private DesktopWindowCatalog Windows => _windows
            ?? throw new InvalidOperationException("ConnectionWindow services must be attached before use.");

        #region Public Methods

        public ConnectionWindow(DockContent panel, string formText = "")
        {
            if (formText == "")
            {
                formText = Language.NewPanel;
            }

            WindowType = WindowType.Connection;
            DockPnl = panel;
            InitializeComponent();
            SetEventHandlers();
            // ReSharper disable once VirtualMemberCallInConstructor
            Text = formText;
            TabText = formText;
            connDock.DocumentStyle = DocumentStyle.DockingWindow;
            connDock.ShowDocumentIcon = true;

            connDock.ActiveContentChanged += ConnDockOnActiveContentChanged;
        }

        public void AttachServices(
            MessageCollector messageCollector,
            ExternalToolsService externalToolsService,
            IConnectionInitiator connectionInitiator,
            ConnectionWorkspaceAdapter connectionWorkspace,
            DesktopWindowCatalog windows)
        {
            _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
            _externalToolsService = externalToolsService ?? throw new ArgumentNullException(nameof(externalToolsService));
            _connectionInitiator = connectionInitiator ?? throw new ArgumentNullException(nameof(connectionInitiator));
            _connectionWorkspace = connectionWorkspace ?? throw new ArgumentNullException(nameof(connectionWorkspace));
            _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        }

        private InterfaceControl GetInterfaceControl()
        {
            return TryGetInterfaceControl()
                ?? throw new InvalidOperationException("The connection window has no embedded interface control.");
        }

        // The main shell owns the outer DockPanel while this window owns the
        // per-connection DockPanel. Expose the active embedded surface so the
        // shell can route keyboard messages to the currently selected session
        // even when the active document is a ConnectionWindow rather than a
        // ConnectionTab.
        internal InterfaceControl? TryGetInterfaceControl() => InterfaceControl.FindInterfaceControl(connDock);

        private void SetEventHandlers()
        {
            SetFormEventHandlers();
            SetContextMenuEventHandlers();
        }

        private void SetFormEventHandlers()
        {
            Load += Connection_Load;
            DockStateChanged += Connection_DockStateChanged;
            FormClosing += Connection_FormClosing;
        }

        private void SetContextMenuEventHandlers()
        {
            // event handler to adjust the items within the context menu
            cmenTab.Opening += ShowHideMenuButtons;

            // event handlers for all context menu items...
            cmenTabFullscreen.Click += (sender, args) => ToggleFullscreen();
            cmenTabSmartSize.Click += (sender, args) => ToggleSmartSize();
            cmenTabViewOnly.Click += (sender, args) => ToggleViewOnly();
            cmenTabTransferFile.Click += (sender, args) => TransferFile();
            cmenTabRefreshScreen.Click += (sender, args) => RefreshScreen();
            cmenTabSendSpecialKeysCtrlAltDel.Click += (sender, args) => SendSpecialKeys(RemoteSpecialKey.CtrlAltDel);
            cmenTabSendSpecialKeysCtrlEsc.Click += (sender, args) => SendSpecialKeys(RemoteSpecialKey.CtrlEsc);
            cmenTabRenameTab.Click += (sender, args) => RenameTab();
            cmenTabDuplicateTab.Click += (sender, args) => DuplicateTab();
            cmenTabReconnect.Click += (sender, args) => Reconnect();
            cmenTabDisconnect.Click += (sender, args) => CloseTabMenu();
            cmenTabDisconnectOthers.Click += (sender, args) => CloseOtherTabs();
            cmenTabDisconnectOthersRight.Click += (sender, args) => CloseOtherTabsToTheRight();
            cmenTabPuttySettings.Click += (sender, args) => ShowPuttySettingsDialog();
            GotFocus += ConnectionWindow_GotFocus;
        }

        private void ConnectionWindow_GotFocus(object? sender, EventArgs e)
        {
            TabHelper.Instance.CurrentPanel = this;
        }

        public ConnectionTab? AddConnectionTab(ConnectionInfo connectionInfo)
        {
            try
            {
                //Set the connection text based on name and preferences
                string titleText;
                if (Properties.OptionsTabsPanelsPage.Default.ShowProtocolOnTabs)
                    titleText = connectionInfo.Protocol + @": ";
                else
                    titleText = "";

                titleText += connectionInfo.Name;

                if (Properties.OptionsTabsPanelsPage.Default.ShowLogonInfoOnTabs)
                {
                    titleText += @" (";
                    if (connectionInfo.Domain != "")
                        titleText += connectionInfo.Domain;

                    if (connectionInfo.Username != "")
                    {
                        if (connectionInfo.Domain != "")
                            titleText += @"\";
                        titleText += connectionInfo.Username;
                    }

                    titleText += @")";
                }

                titleText = titleText.Replace("&", "&&");

                ConnectionTab conTab = new()
                {
                    Tag = connectionInfo,
                    DockAreas = DockAreas.Document | DockAreas.Float,
                    Icon = ConnectionIcon.FromString(ConnectionIcon.GetConnectionDisplayIcon(connectionInfo.Icon)),
                    TabText = titleText,
                    TabPageContextMenuStrip = cmenTab
                };

                // Ensure the ConnectionWindow is visible before adding the tab
                // This prevents visibility issues when the window was created but not yet shown
                // Check DockState instead of Visible to properly detect if window is shown in DockPanel
                if (DockState == DockState.Unknown || DockState == DockState.Hidden || !Visible)
                {
                    ConnectionWorkspace.Show(this);
                }

                //Show the tab
                conTab.Show(connDock, DockState.Document);
                conTab.Focus();
                UpdateConnectionTabChrome();
                return conTab;
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("AddConnectionTab (UI.Window.ConnectionWindow) failed", ex);
            }

            return null;
        }

        #endregion

        public async Task ReconnectAllAsync(IConnectionInitiator initiator)
        {
            List<InterfaceControl> controlList = new();
            try
            {
                foreach (IDockContent dockContent in connDock.DocumentsToArray())
                {
                    if (dockContent is ConnectionTab { Tag: InterfaceControl control })
                        controlList.Add(control);
                }

                foreach (InterfaceControl iControl in controlList)
                {
                    await iControl.Protocol.CloseAsync().ConfigureAwait(true);
                    _ = initiator.OpenConnectionAsync(iControl.Info, ConnectionInfo.Force.DoNotJump);
                }
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("reconnectAll (UI.Window.ConnectionWindow) failed", ex);
            }

        }

        #region Form

        private void Connection_Load(object? sender, EventArgs e)
        {
            ApplyTheme();
            ThemeManager.getInstance().ThemeChanged += ApplyTheme;
            ApplyLanguage();
        }

        private new void ApplyTheme()
        {
            if (!ThemeManager.getInstance().ThemingActive)
            {
                connDock.Theme = ThemeManager.getInstance().DefaultTheme.Theme;
                return;
            }

            base.ApplyTheme();
            try
            {
                connDock.Theme = ThemeManager.getInstance().ActiveTheme.Theme;
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("UI.Window.ConnectionWindow.ApplyTheme() failed", ex);
            }

            _vsToolStripExtender = new VisualStudioToolStripExtender(components)
            {
                DefaultRenderer = _toolStripProfessionalRenderer
            };
            _vsToolStripExtender.SetStyle(cmenTab, ThemeManager.getInstance().ActiveTheme.Version, ThemeManager.getInstance().ActiveTheme.Theme);

            if (!ThemeManager.getInstance().ActiveAndExtended) return;
            connDock.DockBackColor = ThemeManager.getInstance().ActiveTheme.ExtendedPalette.getColor("Tab_Item_Background");
        }

        private bool _documentHandlersAdded;
        private bool _floatHandlersAdded;

        private void Connection_DockStateChanged(object? sender, EventArgs e)
        {
            switch (DockState)
            {
                case DockState.Float:
                    {
                        if (_documentHandlersAdded)
                        {
                            ConnectionWorkspace.MainWindow.ResizeBegin -= Connection_ResizeBegin;
                            ConnectionWorkspace.MainWindow.ResizeEnd -= Connection_ResizeEnd;
                            _documentHandlersAdded = false;
                        }

                        DockHandler.FloatPane.FloatWindow.ResizeBegin += Connection_ResizeBegin;
                        DockHandler.FloatPane.FloatWindow.ResizeEnd += Connection_ResizeEnd;
                        _floatHandlersAdded = true;
                        break;
                    }
                case DockState.Document:
                    {
                        if (_floatHandlersAdded)
                        {
                            DockHandler.FloatPane.FloatWindow.ResizeBegin -= Connection_ResizeBegin;
                            DockHandler.FloatPane.FloatWindow.ResizeEnd -= Connection_ResizeEnd;
                            _floatHandlersAdded = false;
                        }

                        ConnectionWorkspace.MainWindow.ResizeBegin += Connection_ResizeBegin;
                        ConnectionWorkspace.MainWindow.ResizeEnd += Connection_ResizeEnd;
                        _documentHandlersAdded = true;
                        break;
                    }
            }
        }

        private void ApplyLanguage()
        {
            cmenTabFullscreen.Text = Language.Fullscreen;
            cmenTabSmartSize.Text = Language.SmartSize;
            cmenTabViewOnly.Text = Language.ViewOnly;
            cmenTabStartChat.Text = Language.StartChat;
            cmenTabTransferFile.Text = Language.TransferFile;
            cmenTabRefreshScreen.Text = Language.RefreshScreen;
            cmenTabSendSpecialKeys.Text = Language.SendSpecialKeys;
            cmenTabSendSpecialKeysCtrlAltDel.Text = Language.CtrlAltDel;
            cmenTabSendSpecialKeysCtrlEsc.Text = Language.CtrlEsc;
            cmenTabExternalApps.Text = Language._Tools;
            cmenTabRenameTab.Text = Language.RenameTab;
            cmenTabDuplicateTab.Text = Language.DuplicateTab;
            cmenTabReconnect.Text = Language.Reconnect;
            cmenTabDisconnect.Text = Language.Disconnect;
            cmenTabDisconnectOthers.Text = Language.DisconnectOthers;
            cmenTabDisconnectOthersRight.Text = Language.DisconnectOthersRight;
            cmenTabPuttySettings.Text = Language.PuttySettings;
        }

        private void Connection_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!ConnectionWorkspace.MainWindow.IsClosing &&
                (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseMode.All & connDock.Documents.Any() ||
                 Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseMode.Multiple &
                 connDock.Documents.Count() > 1))
            {
                DialogResult result = CTaskDialog.MessageBox(this, GeneralAppInfo.ProductName ?? string.Empty, FormatText(Language.ConfirmCloseConnectionPanelMainInstruction, Text), "", "", "", Language.CheckboxDoNotShowThisMessageAgain, ETaskDialogButtons.YesNo, ESysIcons.Question, ESysIcons.Question);
                if (CTaskDialog.VerificationChecked)
                {
                    Settings.Default.ConfirmCloseConnection = (int)ConfirmCloseMode.Never;
                    Settings.Default.Save();
                }

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            try
            {
                foreach (IDockContent dockContent in connDock.Documents.ToArray())
                {
                    ConnectionTab tabP = (ConnectionTab)dockContent;
                    if (tabP.Tag == null) continue;
                    tabP.silentClose = true;
                    tabP.Close();
                }
                UpdateConnectionTabChrome();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("UI.Window.Connection.Connection_FormClosing() failed", ex);
            }
        }

        public new event EventHandler? ResizeBegin;

        private void Connection_ResizeBegin(object? sender, EventArgs e)
        {
            ResizeBegin?.Invoke(this, e);
        }

        public new event EventHandler? ResizeEnd;

        private void Connection_ResizeEnd(object? sender, EventArgs e)
        {
            ResizeEnd?.Invoke(sender, e);
        }

        internal void NavigateToNextTab()
        {
            try
            {
                var documents = connDock.DocumentsToArray();
                if (documents.Length <= 1) return;

                var currentIndex = Array.IndexOf(documents, connDock.ActiveContent);
                if (currentIndex == -1)
                {
                    MessageCollector.AddMessage(MessageClass.DebugMsg, "NavigateToNextTab: ActiveContent not found in documents array");
                    return;
                }

                if (!ConnectionTabNavigator.TryGetRelativeIndex(documents.Length, currentIndex, 1, out int nextIndex))
                    return;

                documents[nextIndex].DockHandler.Activate();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("NavigateToNextTab (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        internal void NavigateToPreviousTab()
        {
            try
            {
                var documents = connDock.DocumentsToArray();
                if (documents.Length <= 1) return;

                var currentIndex = Array.IndexOf(documents, connDock.ActiveContent);
                if (currentIndex == -1)
                {
                    MessageCollector.AddMessage(MessageClass.DebugMsg, "NavigateToPreviousTab: ActiveContent not found in documents array");
                    return;
                }

                if (!ConnectionTabNavigator.TryGetRelativeIndex(documents.Length, currentIndex, -1, out int previousIndex))
                    return;

                documents[previousIndex].DockHandler.Activate();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("NavigateToPreviousTab (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        internal void NavigateToTab(int index)
        {
            try
            {
                var documents = connDock.DocumentsToArray();
                if (!ConnectionTabNavigator.IsValidIndex(documents.Length, index)) return;

                documents[index].DockHandler.Activate();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("NavigateToTab (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        internal IDockContent[] GetDocuments()
        {
            try
            {
                return connDock.DocumentsToArray();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("GetDocuments (UI.Window.ConnectionWindow) failed", ex);
                return Array.Empty<IDockContent>();
            }
        }

        #endregion

        // A ConnectionWindow is itself the shell tab. Showing a second tab
        // strip for a single embedded SSH/RDP session wastes client height and
        // makes the native surface appear inset. Keep the inner strip only
        // when there are multiple sessions to switch between.
        private void UpdateConnectionTabChrome()
        {
            DocumentStyle style = connDock.Documents.Count() > 1
                ? DocumentStyle.DockingWindow
                : DocumentStyle.DockingSdi;
            if (connDock.DocumentStyle != style)
                connDock.DocumentStyle = style;
        }

        #region Events

        private void ConnDockOnActiveContentChanged(object? sender, EventArgs e)
        {
            // DockPanel raises this event while a document is being created or
            // torn down. During that transition there is no embedded protocol
            // control yet; message/focus routing must simply wait for the next
            // active-content notification instead of throwing on the UI thread.
            InterfaceControl? ic = TryGetInterfaceControl();
            if (ic?.Info is null) return;
            ConnectionWorkspace.Select(ic.Info);

            foreach (IDockContent document in connDock.DocumentsToArray())
            {
                if (document is not ConnectionTab tab) continue;
                InterfaceControl? candidate = InterfaceControl.FindInterfaceControl(tab);
                candidate?.RemoteResourceBar?.SetIsActive(ReferenceEquals(candidate, ic));
            }

            FocusActiveProtocolAfterDocking(ic);
        }

        private void FocusActiveProtocolAfterDocking(InterfaceControl expectedInterface)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            BeginInvoke((Action)(() =>
            {
                if (IsDisposed || !ReferenceEquals(TryGetInterfaceControl(), expectedInterface))
                    return;

                expectedInterface.Protocol.Focus();
            }));
        }

        #endregion

        #region Tab Menu

        private void ShowHideMenuButtons(object? sender, CancelEventArgs e)
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl == null) return;

                if (interfaceControl.Protocol is IViewOnlySession viewOnly)
                {
                    cmenTabViewOnly.Visible = true;
                    cmenTabViewOnly.Checked = viewOnly.ViewOnly;
                }
                else
                {
                    cmenTabViewOnly.Visible = false;
                }

                if (interfaceControl.Protocol is IFullscreenSession fullscreen &&
                    interfaceControl.Protocol is ISmartSizingSession smartSizing)
                {
                    cmenTabFullscreen.Visible = true;
                    cmenTabFullscreen.Checked = fullscreen.Fullscreen;
                    cmenTabSmartSize.Visible = true;
                    cmenTabSmartSize.Checked = smartSizing.SmartSize;
                }
                else
                {
                    cmenTabFullscreen.Visible = false;
                    cmenTabSmartSize.Visible = false;
                }

                if (interfaceControl.Protocol is IRemoteSpecialKeysController)
                {
                    cmenTabSendSpecialKeys.Visible = true;
                    cmenTabSmartSize.Visible = true;
                    cmenTabStartChat.Visible = false;
                    cmenTabRefreshScreen.Visible = true;
                    cmenTabTransferFile.Visible = false;
                }
                else
                {
                    cmenTabSendSpecialKeys.Visible = false;
                    cmenTabStartChat.Visible = false;
                    cmenTabRefreshScreen.Visible = false;
                    cmenTabTransferFile.Visible = false;
                }

                if (interfaceControl.Info.Protocol == ProtocolKind.Ssh1 |
                    interfaceControl.Info.Protocol == ProtocolKind.Ssh2)
                {
                    cmenTabTransferFile.Visible = true;
                }

                cmenTabPuttySettings.Visible = interfaceControl.Protocol is IPuttySettingsSession;

                AddExternalApps();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("ShowHideMenuButtons (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        #endregion

        #region Tab Actions

        private void ToggleSmartSize()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();

                if (interfaceControl?.Protocol is ISmartSizingSession smartSizing)
                    smartSizing.ToggleSmartSize();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("ToggleSmartSize (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void TransferFile()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl == null) return;

                if (interfaceControl.Info.Protocol == ProtocolKind.Ssh1 |
                    interfaceControl.Info.Protocol == ProtocolKind.Ssh2)
                    SshTransferFile();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("TransferFile (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void SshTransferFile()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl == null) return;

                Windows.Show(WindowType.SSHTransfer);
                ConnectionInfo connectionInfo = interfaceControl.Info;

                Windows.SshtransferForm.Hostname = connectionInfo.Hostname;
                Windows.SshtransferForm.Username = connectionInfo.Username;
                //App.Windows.SshtransferForm.Password = connectionInfo.Password.ConvertToUnsecureString();
                Windows.SshtransferForm.Password = connectionInfo.Password;
                Windows.SshtransferForm.Port = Convert.ToString(connectionInfo.Port, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("SSHTransferFile (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void ToggleViewOnly()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (!(interfaceControl?.Protocol is IViewOnlySession viewOnly))
                    return;

                cmenTabViewOnly.Checked = !cmenTabViewOnly.Checked;
                viewOnly.ToggleViewOnly();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("ToggleViewOnly (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void RefreshScreen()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl?.Protocol is IRemoteScreenController screen)
                    screen.RefreshScreen();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("RefreshScreen (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void SendSpecialKeys(RemoteSpecialKey key)
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl?.Protocol is IRemoteSpecialKeysController specialKeys)
                    specialKeys.SendSpecialKeys(key);
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("SendSpecialKeys (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void ToggleFullscreen()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl?.Protocol is IFullscreenSession fullscreen)
                    fullscreen.ToggleFullscreen();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("ToggleFullscreen (UI.Window.ConnectionWindow) failed",
                                                             ex);
            }
        }

        private void ShowPuttySettingsDialog()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl?.Protocol is IPuttySettingsSession puttySettings)
                    puttySettings.ShowSettingsDialog();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage(
                                                             "ShowPuttySettingsDialog (UI.Window.ConnectionWindow) failed",
                                                             ex);
            }
        }

        private void AddExternalApps()
        {
            try
            {
                //clean up. since new items are added below, we have to dispose of any previous items first
                if (cmenTabExternalApps.DropDownItems.Count > 0)
                {
                    for (int i = cmenTabExternalApps.DropDownItems.Count - 1; i >= 0; i--)
                        cmenTabExternalApps.DropDownItems[i].Dispose();
                    cmenTabExternalApps.DropDownItems.Clear();
                }

                //add ext apps
                foreach (ExternalTool externalTool in ExternalToolsService.ExternalTools)
                {
                    ToolStripMenuItem nItem = new()
                    {
                        Text = externalTool.DisplayName,
                        Tag = externalTool,
                        /* rare failure here. While ExternalTool.Image already tries to default this
                         * try again so it's not null/doesn't crash.
                         */
                        Image = externalTool.Image ?? Properties.Resources.LoipvRemote_Icon.ToBitmap()
                    };

                    nItem.Click += (sender, args) =>
                    {
                        if (sender is ToolStripMenuItem { Tag: ExternalTool tool })
                            StartExternalApp(tool);
                    };
                    cmenTabExternalApps.DropDownItems.Add(nItem);
                }
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace("cMenTreeTools_DropDownOpening failed (UI.Window.ConnectionWindow)", ex);
            }
        }

        private void StartExternalApp(ExternalTool externalTool)
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                externalTool.Start(interfaceControl?.Info);
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("cmenTabExternalAppsEntry_Click failed (UI.Window.ConnectionWindow)", ex);
            }
        }


        private void CloseTabMenu()
        {
            if (GetInterfaceControl()?.Parent is not ConnectionTab selectedTab) return;

            try
            {
                selectedTab.Close();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("CloseTabMenu (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void CloseOtherTabs()
        {
            if (GetInterfaceControl()?.Parent is not ConnectionTab selectedTab) return;
            if (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseMode.Multiple)
            {
                DialogResult result = CTaskDialog.MessageBox(this, GeneralAppInfo.ProductName,
                                                    FormatText(Language.ConfirmCloseConnectionOthersInstruction,
                                                                  selectedTab.TabText), "", "", "",
                                                    Language.CheckboxDoNotShowThisMessageAgain,
                                                    ETaskDialogButtons.YesNo, ESysIcons.Question,
                                                    ESysIcons.Question);
                if (CTaskDialog.VerificationChecked)
                {
                    Settings.Default.ConfirmCloseConnection = (int)ConfirmCloseMode.Never;
                    Settings.Default.Save();
                }

                if (result == DialogResult.No)
                {
                    return;
                }
            }

            foreach (IDockContent dockContent in connDock.Documents.ToArray())
            {
                ConnectionTab tab = (ConnectionTab)dockContent;
                if (selectedTab != tab)
                {
                    tab.Close();
                }
            }
        }

        private void CloseOtherTabsToTheRight()
        {
            try
            {
                if (GetInterfaceControl()?.Parent is not ConnectionTab selectedTab) return;
                DockPane dockPane = selectedTab.Pane;

                bool pastTabToKeepAlive = false;
                List<ConnectionTab> connectionsToClose = new();
                foreach (IDockContent dockContent in dockPane.Contents)
                {
                    ConnectionTab tab = (ConnectionTab)dockContent;
                    if (pastTabToKeepAlive)
                        connectionsToClose.Add(tab);

                    if (selectedTab == tab)
                        pastTabToKeepAlive = true;
                }

                foreach (ConnectionTab tab in connectionsToClose)
                {
                    tab.Close();
                }
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("CloseTabMenu (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void DuplicateTab()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl == null) return;
                _ = ConnectionInitiator.OpenConnectionAsync(interfaceControl.Info, ConnectionInfo.Force.DoNotJump);
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("DuplicateTab (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void Reconnect()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl == null)
                {
                    MessageCollector.AddMessage(MessageClass.WarningMsg, "Reconnect (UI.Window.ConnectionWindow) failed. Could not find InterfaceControl.");
                    return;
                }

                Invoke(new Action(() => OnProtocolClosed(interfaceControl.Protocol)));
                _ = ConnectionInitiator.OpenConnectionAsync(interfaceControl.Info, ConnectionInfo.Force.DoNotJump);
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("Reconnect (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void RenameTab()
        {
            try
            {
                if (GetInterfaceControl() is not { Parent: ConnectionTab selectedTab }) return;
                using (FrmInputBox frmInputBox = new(Language.NewTitle, Language.NewTitle, selectedTab.TabText))
                {
                    DialogResult dr = frmInputBox.ShowDialog();
                    if (dr != DialogResult.OK) return;
                    if (!string.IsNullOrEmpty(frmInputBox.returnValue))
                        selectedTab.TabText = frmInputBox.returnValue.Replace("&", "&&");
                }
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage("RenameTab (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        #endregion

        #region Protocols

        public void OnProtocolClosed(object? sender)
        {
            if (sender is not ProtocolSessionBridge protocolBase ||
                protocolBase.InterfaceControl is not { Parent: ConnectionTab tabPage }) return;
            if (tabPage.Disposing || tabPage.IsDisposed) return;
            if (IsDisposed || Disposing) return;

            try
            {
                void CloseTab()
                {
                    if (!tabPage.IsDisposed && !tabPage.Disposing && !Disposing && !IsDisposed)
                    {
                        tabPage.protocolClose = true;
                        tabPage.Close();
                    }
                }

                if (tabPage.InvokeRequired)
                    tabPage.BeginInvoke((Action)CloseTab);
                else
                    CloseTab();
            }
            catch (ObjectDisposedException)
            {
                // A close event can race with form teardown.
            }
            catch (InvalidOperationException)
            {
                // The handle may disappear between the state check and BeginInvoke.
            }
        }

        public void OnProtocolTitleChanged(object? sender, string newTitle)
        {
            if (!Properties.OptionsTabsPanelsPage.Default.UseTerminalTitleForTabs) return;
            if (sender is not ProtocolSessionBridge protocolBase ||
                protocolBase.InterfaceControl is not { Parent: ConnectionTab tabPage }) return;
            if (tabPage.Disposing || tabPage.IsDisposed) return;
            if (IsDisposed || Disposing) return;

            string connectionName = protocolBase.InterfaceControl.Info?.Name ?? string.Empty;
            string tabText = TerminalTitleFormatter.Format(newTitle, connectionName);

            if (tabPage.InvokeRequired)
            {
                if (tabPage.IsHandleCreated)
                    tabPage.BeginInvoke(new Action(() =>
                    {
                        if (!tabPage.IsDisposed && !tabPage.Disposing)
                            tabPage.TabText = tabText;
                    }));
            }
            else
                tabPage.TabText = tabText;
        }

        #endregion
    }
}
