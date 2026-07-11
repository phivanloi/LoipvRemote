#region Usings
using Microsoft.Win32;
using LoipvRemote.App;
using LoipvRemote.App.Info;
using LoipvRemote.App.Initialization;
using LoipvRemote.Config;
using LoipvRemote.Config.Connections;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Putty;
using LoipvRemote.Config.Settings;
using LoipvRemote.Connection;
using LoipvRemote.Connection.Protocol;
using LoipvRemote.Messages;
using LoipvRemote.Messages.MessageWriters;
using LoipvRemote.Themes;
using LoipvRemote.Tools;
using LoipvRemote.UI.Menu;
using LoipvRemote.UI.Tabs;
using LoipvRemote.UI.TaskDialog;
using LoipvRemote.UI.Window;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LoipvRemote.UI.Panels;
using WeifenLuo.WinFormsUI.Docking;
using LoipvRemote.UI.Controls;
using LoipvRemote.UI.DesignSystem;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;
using LoipvRemote.Config.Settings.Registry;
using System.Threading; // ADDED
#endregion

// ReSharper disable MemberCanBePrivate.Global

namespace LoipvRemote.UI.Forms
{
    [SupportedOSPlatform("windows")]
    public partial class FrmMain
    {
        // CHANGED: lazy, thread-safe, STA-enforced initialization
        private static readonly Lazy<FrmMain> s_default =
            new(InitializeOnSta, LazyThreadSafetyMode.ExecutionAndPublication);

        public static FrmMain Default => s_default.Value;

        public static bool IsCreated => s_default.IsValueCreated;

        private static FrmMain InitializeOnSta()
        {
            // Enforce STA to avoid OLE/WinForms threading violations
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                // If we're already on a WinForms UI thread with a sync context, marshal to it
                if (SynchronizationContext.Current is WindowsFormsSynchronizationContext ctx)
                {
                    FrmMain created = null;
                    ctx.Send(_ => created = new FrmMain(), null);
                    return created!;
                }

                throw new ThreadStateException("FrmMain must be created on an STA thread.");
            }

            return new FrmMain();
        }

        private static ClipboardchangeEventHandler _clipboardChangedEvent;
        private bool _inSizeMove;
        private bool _inMouseActivate;
        private IntPtr _fpChainedWindowHandle;
        private bool _usingSqlServer;
        private string _connectionsFileName;
        private bool _showFullPathInTitle;
        private readonly AdvancedWindowMenu _advancedWindowMenu;
        private ConnectionInfo _selectedConnection;
        private readonly IList<IMessageWriter> _messageWriters = [];
        private readonly ThemeManager _themeManager;
        private readonly FileBackupPruner _backupPruner = new();
        private readonly UnifiedWindowHeader _unifiedWindowHeader;
        private System.Windows.Forms.Timer? _startupMaximizeTimer;
        public static FrmOptions OptionsForm;

        /// <summary>
        /// Recreates the OptionsForm if it has been disposed.
        /// This method should be called when OptionsForm is in an invalid state.
        /// </summary>
        public static void RecreateOptionsForm()
        {
            Logger.Instance.Log?.Debug("[FrmMain.RecreateOptionsForm] Recreating OptionsForm");

            // Dispose the old form if it exists
            if (OptionsForm != null && !OptionsForm.IsDisposed)
            {
                Logger.Instance.Log?.Debug("[FrmMain.RecreateOptionsForm] Disposing old OptionsForm");
                OptionsForm.Dispose();
            }

            // Create a new instance
            OptionsForm = new FrmOptions();
            Logger.Instance.Log?.Debug("[FrmMain.RecreateOptionsForm] New OptionsForm created");
        }

        internal FullscreenHandler Fullscreen { get; set; }

        //Added theming support
        private readonly ToolStripRenderer _toolStripProfessionalRenderer = new ToolStripProfessionalRenderer();

        private FrmMain()
        {
            _showFullPathInTitle = Properties.OptionsAppearancePage.Default.ShowCompleteConsPathInTitle;
            InitializeComponent();
            FormBorderStyle = FormBorderStyle.None;
            Padding = Padding.Empty;
            _unifiedWindowHeader = new UnifiedWindowHeader(this, msMain);
            Controls.Add(_unifiedWindowHeader);
            Controls.SetChildIndex(_unifiedWindowHeader, 0);
            ConfigureEdgeToEdgeDocking();
            ClientSizeChanged += (_, _) => LayoutUnifiedShell();
            _unifiedWindowHeader.SizeChanged += (_, _) => LayoutUnifiedShell();
            LocationChanged += (_, _) => UpdateMaximizedBounds();
            UpdateMaximizedBounds();
            LayoutUnifiedShell();

            Screen targetScreen = (Screen.AllScreens.Length > 1) ? Screen.AllScreens[1] : Screen.AllScreens[0];

            Rectangle viewport = targetScreen.WorkingArea;

            // normally it should be screens[1] however due DPI apply 1 size "same" as default with 100%
            this.Left = viewport.Left + (targetScreen.Bounds.Size.Width / 2) - (this.Width / 2);
            this.Top = viewport.Top + (targetScreen.Bounds.Size.Height / 2) - (this.Height / 2);

            Fullscreen = new FullscreenHandler(this);
            Fullscreen.ValueChanged += (_, _) =>
            {
                _unifiedWindowHeader.Visible = !Fullscreen.Value;
                LayoutUnifiedShell();
            };

            //Theming support
            _themeManager = ThemeManager.getInstance();
            vsToolStripExtender.DefaultRenderer = _toolStripProfessionalRenderer;
            ApplyTheme();

            _advancedWindowMenu = new AdvancedWindowMenu(this);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                createParams.Style = WindowTaskbarStyle.AddStandardTaskbarCommands(createParams.Style);
                return createParams;
            }
        }

        #region Properties

        public FormWindowState PreviousWindowState { get; set; }

        public bool IsClosing { get; private set; }

        public bool AreWeUsingSqlServerForSavingConnections
        {
            get => _usingSqlServer;
            set
            {
                if (_usingSqlServer == value)
                {
                    return;
                }

                _usingSqlServer = value;
                UpdateWindowTitle();
            }
        }

        public string ConnectionsFileName
        {
            get => _connectionsFileName;
            set
            {
                if (_connectionsFileName == value)
                {
                    return;
                }

                _connectionsFileName = value;
                UpdateWindowTitle();
            }
        }

        public bool ShowFullPathInTitle
        {
            get => _showFullPathInTitle;
            set
            {
                if (_showFullPathInTitle == value)
                {
                    return;
                }

                _showFullPathInTitle = value;
                UpdateWindowTitle();
            }
        }

        public ConnectionInfo SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                if (_selectedConnection == value)
                {
                    return;
                }

                _selectedConnection = value;
                UpdateWindowTitle();
            }
        }

        #endregion

        #region Startup & Shutdown

        private void FrmMain_Load(object sender, EventArgs e)
        {
            MessageCollector messageCollector = Runtime.MessageCollector;

            SettingsLoader settingsLoader = new(this, messageCollector, _quickConnectToolStrip, _externalToolsToolStrip, _multiSshToolStrip);
            settingsLoader.LoadSettings();

            MessageCollectorSetup.SetupMessageCollector(messageCollector, _messageWriters);
            MessageCollectorSetup.BuildMessageWritersFromSettings(_messageWriters);

            Startup.Instance.InitializeProgram(messageCollector);

            SetMenuDependencies();

            DockPanelLayoutLoader uiLoader = new(this, messageCollector);
            uiLoader.LoadPanelsFromXml();

            LockToolbarPositions(Properties.Settings.Default.LockToolbars);
            Properties.Settings.Default.PropertyChanged += OnApplicationSettingChanged;

            _themeManager.ThemeChanged += ApplyTheme;

            _fpChainedWindowHandle = NativeMethods.SetClipboardViewer(Handle);

            Runtime.WindowList = [];

            if (Properties.App.Default.ResetPanels)
                SetDefaultLayout();
            else
                SetLayout();

            ApplyStartupShellLayout();
            ShowHidePanelTabs();

            Runtime.ConnectionsService.ConnectionsLoaded += ConnectionsServiceOnConnectionsLoaded;
            Runtime.ConnectionsService.ConnectionsSaved += ConnectionsServiceOnConnectionsSaved;

            // Close splash screen before loading connections to ensure password dialog appears on top
            ProgramRoot.CloseSplash();

            CredsAndConsSetup credsAndConsSetup = new();
            credsAndConsSetup.LoadCredsAndCons();

            // Initialize panel binding for Connections and Config panels
            UI.Panels.PanelBinder.Instance.Initialize();

            AppWindows.TreeForm.Focus();

            PuttySessionsManager.Instance.StartWatcher();

            Startup.Instance.CreateConnectionsProvider(messageCollector);

            _advancedWindowMenu.BuildAdditionalMenuItems();
            SystemEvents.DisplaySettingsChanged += _advancedWindowMenu.OnDisplayChanged;
            ApplyLanguage();

            UiScaleManager.Instance.Apply(this);

            Opacity = 1;
            //Fix MagicRemove , revision on panel strategy for mdi

            pnlDock.ShowDocumentIcon = true;

            if (Properties.OptionsStartupExitPage.Default.StartMinimized)
            {
                WindowState = FormWindowState.Minimized;
                if (Properties.OptionsAppearancePage.Default.MinimizeToTray)
                    ShowInTaskbar = false;
            }
            if (Properties.OptionsStartupExitPage.Default.StartFullScreen)
            {
                Fullscreen.Value = true;
            }

            OptionsForm = new FrmOptions();

            if (!Properties.OptionsTabsPanelsPage.Default.CreateEmptyPanelOnStartUp)
            {
                return;
            }
            string panelName = !string.IsNullOrEmpty(Properties.OptionsTabsPanelsPage.Default.StartUpPanelName) ? Properties.OptionsTabsPanelsPage.Default.StartUpPanelName : Language.NewPanel;

            PanelAdder panelAdder = new();
            if (!panelAdder.DoesPanelExist(panelName))
                panelAdder.AddPanel(panelName);
        }

        private void ApplyLanguage()
        {
            fileMenu.ApplyLanguage();
            sessionsMenu.ApplyLanguage();
            viewMenu.ApplyLanguage();
            toolsMenu.ApplyLanguage();
        }

        private void OnApplicationSettingChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case nameof(Properties.Settings.LockToolbars):
                    LockToolbarPositions(Properties.Settings.Default.LockToolbars);
                    break;
                case nameof(Properties.Settings.ViewMenuExternalTools):
                    LockToolbarPositions(Properties.Settings.Default.LockToolbars);
                    break;
                case nameof(Properties.Settings.ViewMenuMessages):
                    LockToolbarPositions(Properties.Settings.Default.LockToolbars);
                    break;
                case nameof(Properties.Settings.ViewMenuMultiSSH):
                    LockToolbarPositions(Properties.Settings.Default.LockToolbars);
                    break;
                case nameof(Properties.Settings.ViewMenuQuickConnect):
                    LockToolbarPositions(Properties.Settings.Default.LockToolbars);
                    break;
                default:
                    return;
            }
        }

        private void LockToolbarPositions(bool shouldBeLocked)
        {
            ToolStrip[] toolbars = [_quickConnectToolStrip, _multiSshToolStrip, _externalToolsToolStrip];
            foreach (ToolStrip toolbar in toolbars)
            {
                toolbar.GripStyle = shouldBeLocked ? ToolStripGripStyle.Hidden : ToolStripGripStyle.Visible;
            }
        }

        private void ConnectionsServiceOnConnectionsLoaded(object? sender, ConnectionsLoadedEventArgs connectionsLoadedEventArgs)
        {
            UpdateWindowTitle();
        }

        private void ConnectionsServiceOnConnectionsSaved(object sender, ConnectionsSavedEventArgs connectionsSavedEventArgs)
        {
            if (connectionsSavedEventArgs.UsingDatabase)
                return;

            _backupPruner.PruneBackupFiles(connectionsSavedEventArgs.ConnectionFileName, Properties.OptionsBackupPage.Default.BackupFileKeepCount);
        }

        private void SetMenuDependencies()
        {
            fileMenu.TreeWindow = AppWindows.TreeForm;

            viewMenu.TsExternalTools = _externalToolsToolStrip;
            viewMenu.TsQuickConnect = _quickConnectToolStrip;
            viewMenu.TsMultiSsh = _multiSshToolStrip;
            viewMenu.FullscreenHandler = Fullscreen;
            viewMenu.MainForm = this;

            toolsMenu.MainForm = this;
            toolsMenu.CredentialProviderCatalog = Runtime.CredentialProviderCatalog;
        }

        // Apply the dark/light title bar before the window is shown to avoid a white flash.
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _themeManager.ApplyThemeToTitleBar(this);
        }

        //Theming support
        private void ApplyTheme()
        {
            _themeManager.ApplyThemeToTitleBar(this);

            if (!_themeManager.ThemingActive)
            {
                pnlDock.Theme = _themeManager.DefaultTheme.Theme;
                ConfigureEdgeToEdgeDocking();
                _unifiedWindowHeader.ApplyTheme(SystemColors.Control, SystemColors.ControlText);
                return;
            }

            try
            {
                // this will always throw when turning themes on from
                // the options menu.
                pnlDock.Theme = _themeManager.ActiveTheme.Theme;
            }
            catch (Exception)
            {
                // intentionally ignore exception
            }

            ConfigureEdgeToEdgeDocking();

            // Persist settings when rebuilding UI
            try
            {
                vsToolStripExtender.SetStyle(msMain, _themeManager.ActiveTheme.Version, _themeManager.ActiveTheme.Theme);
                vsToolStripExtender.SetStyle(_quickConnectToolStrip, _themeManager.ActiveTheme.Version, _themeManager.ActiveTheme.Theme);
                vsToolStripExtender.SetStyle(_externalToolsToolStrip, _themeManager.ActiveTheme.Version, _themeManager.ActiveTheme.Theme);
                vsToolStripExtender.SetStyle(_multiSshToolStrip, _themeManager.ActiveTheme.Version, _themeManager.ActiveTheme.Theme);

                if (!_themeManager.ActiveAndExtended) return;
                tsContainer.TopToolStripPanel.BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("CommandBarMenuDefault_Background");
                BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
                ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");
                _unifiedWindowHeader.ApplyTheme(tsContainer.TopToolStripPanel.BackColor, ForeColor);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace("Error applying theme", ex, MessageClass.WarningMsg);
            }
        }

        private void FrmMain_Shown(object sender, EventArgs e)
        {
            ActivateStartupWindow();
            BeginInvoke((MethodInvoker)ActivateStartupWindow);

            _startupMaximizeTimer?.Dispose();
            _startupMaximizeTimer = new System.Windows.Forms.Timer
            {
                Interval = 100
            };
            _startupMaximizeTimer.Tick += (_, _) =>
            {
                _startupMaximizeTimer.Stop();
                _startupMaximizeTimer.Dispose();
                _startupMaximizeTimer = null;
                if (!IsDisposed && Visible)
                    WindowState = FormWindowState.Maximized;
            };
            _startupMaximizeTimer.Start();

        }

        private void ActivateStartupWindow()
        {
            if (IsDisposed || !Visible) return;

            TopMost = true;
            Activate();
            BringToFront();
            NativeMethods.SetForegroundWindow(Handle);
            TopMost = false;
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Runtime.WindowList != null)
            {
                foreach (BaseWindow window in Runtime.WindowList)
                {
                    window.Close();
                }
            }

            IsClosing = true;

            Hide();

            if (Properties.OptionsAppearancePage.Default.CloseToTray)
            {
                Runtime.NotificationAreaIcon ??= new NotificationAreaIcon();

                if (WindowState == FormWindowState.Normal || WindowState == FormWindowState.Maximized)
                {
                    Hide();
                    WindowState = FormWindowState.Minimized;
                    e.Cancel = true;
                    return;
                }
            }

            if (!(Runtime.WindowList == null || Runtime.WindowList.Count == 0))
            {
                int openConnections = 0;
                if (pnlDock.Contents.Count > 0)
                {
                    foreach (IDockContent dc in pnlDock.Contents)
                    {
                        if (dc is not ConnectionWindow cw) continue;
                        if (cw.Controls.Count < 1) continue;
                        if (cw.Controls[0] is not DockPanel dp) continue;
                        if (dp.Contents.Count > 0)
                            openConnections += dp.Contents.Count;
                    }
                }

                if (openConnections > 0 &&
                    (Properties.Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.All |
                     (Properties.Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.Multiple &
                      openConnections > 1) || Properties.Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.Exit))
                {
                    DialogResult result = CTaskDialog.MessageBox(this, Application.ProductName, Language.ConfirmExitMainInstruction, "", "", "", Language.CheckboxDoNotShowThisMessageAgain, ETaskDialogButtons.YesNo, ESysIcons.Question, ESysIcons.Question);
                    if (CTaskDialog.VerificationChecked)
                    {
                        Properties.Settings.Default.ConfirmCloseConnection = (int)ConfirmCloseEnum.Never;
                    }

                    if (result == DialogResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }

            NativeMethods.ChangeClipboardChain(Handle, _fpChainedWindowHandle);
            SystemEvents.DisplaySettingsChanged -= _advancedWindowMenu.OnDisplayChanged;
            Shutdown.Cleanup(_quickConnectToolStrip, _externalToolsToolStrip, _multiSshToolStrip, this);

            Debug.Print("[END] - " + Convert.ToString(DateTime.Now, CultureInfo.InvariantCulture));
        }

        #endregion

        #region Timer

        private void TmrAutoSave_Tick(object sender, EventArgs e)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, "Doing AutoSave");
            Runtime.ConnectionsService.SaveConnectionsAsync();
        }

        #endregion

        #region Window Overrides and DockPanel Stuff

        private void FrmMain_ResizeBegin(object sender, EventArgs e)
        {
            _inSizeMove = true;
        }

        private void FrmMain_Resize(object sender, EventArgs e)
        {
            UpdateMaximizedBounds();
            if (_unifiedWindowHeader is not null)
                _unifiedWindowHeader.Visible = Fullscreen?.Value != true;
            LayoutUnifiedShell();
            if (WindowState == FormWindowState.Minimized)
            {
                if (!Properties.OptionsAppearancePage.Default.MinimizeToTray) return;
                Runtime.NotificationAreaIcon ??= new NotificationAreaIcon();

                Hide();
            }
            else
            {
                PreviousWindowState = WindowState;
            }
        }

        private void FrmMain_ResizeEnd(object sender, EventArgs e)
        {
            _inSizeMove = false;
            // This handles activations from clicks that started a size/move operation
            ActivateConnection();
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (PuttyImeMessageRouter.ShouldForward(m.Msg))
            {
                InterfaceControl activeInterface = InterfaceControl.FindInterfaceControl(pnlDock);
                if (activeInterface?.Protocol is PuttyBase putty && putty.PuttyHandle != IntPtr.Zero)
                {
                    NativeMethods.SendMessage(putty.PuttyHandle, (uint)m.Msg, m.WParam, m.LParam);
                    return;
                }
            }

            if (m.Msg == NativeMethods.WM_NCHITTEST)
            {
                base.WndProc(ref m);
                ApplyUnifiedWindowChromeHitTest(ref m);
                return;
            }

            if (m.Msg == NativeMethods.WM_NCLBUTTONDBLCLK &&
                WindowChromeHitTest.IsCaptionDoubleClick(m.WParam.ToInt32()))
            {
                _unifiedWindowHeader.ToggleMaximizeRestore();
                return;
            }

            // Listen for and handle operating system messages
            try
            {
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (m.Msg)
                {
                    case NativeMethods.WM_MOUSEACTIVATE:
                        _inMouseActivate = true;
                        break;
                    case NativeMethods.WM_ACTIVATEAPP:
                        Control candidateTabToFocus = FromChildHandle(NativeMethods.WindowFromPoint(MousePosition))
                                               ?? GetChildAtPoint(MousePosition);
                        if (candidateTabToFocus is InterfaceControl) candidateTabToFocus.Parent.Focus();
                        _inMouseActivate = false;
                        break;
                    case NativeMethods.WM_ACTIVATE:
                        // Only handle this msg if it was triggered by a click
                        if (NativeMethods.LOWORD(m.WParam) == NativeMethods.WA_CLICKACTIVE)
                        {
                            Control controlThatWasClicked = FromChildHandle(NativeMethods.WindowFromPoint(MousePosition))
                                                     ?? GetChildAtPoint(MousePosition);
                            if (controlThatWasClicked != null)
                            {
                                if (controlThatWasClicked is TreeView ||
                                    controlThatWasClicked is ComboBox ||
                                    controlThatWasClicked is MrngTextBox ||
                                    controlThatWasClicked is FrmMain)
                                {
                                    controlThatWasClicked.Focus();
                                }
                                else if (controlThatWasClicked.CanSelect ||
                                         controlThatWasClicked is MenuStrip ||
                                         controlThatWasClicked is ToolStrip)
                                {
                                    // Simulate a mouse event since one wasn't generated by Windows
                                    SimulateClick(controlThatWasClicked);
                                    controlThatWasClicked.Focus();
                                }
                                else if (controlThatWasClicked is AutoHideStripBase)
                                {
                                    // only focus the autohide toolstrip
                                    controlThatWasClicked.Focus();
                                }
                                else
                                {
                                    // This handles activations from clicks that did not start a size/move operation
                                    ActivateConnection();
                                }
                            }
                        }
                        break;
                    case NativeMethods.WM_WINDOWPOSCHANGED:
                        // Ignore this message if the window wasn't activated
                        NativeMethods.WINDOWPOS windowPos =
                            (NativeMethods.WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(NativeMethods.WINDOWPOS));
                        if ((windowPos.flags & NativeMethods.SWP_NOACTIVATE) == 0)
                        {
                            if (!_inMouseActivate && !_inSizeMove)
                                ActivateConnection();
                        }
                        break;
                    case NativeMethods.WM_SYSCOMMAND:
                        Screen screen = _advancedWindowMenu.GetScreenById(m.WParam.ToInt32());
                        if (screen != null)
                        {
                            Screens.SendFormToScreen(screen);
                            Console.WriteLine(_advancedWindowMenu.GetScreenById(m.WParam.ToInt32()).ToString());
                        }
                        break;
                    case NativeMethods.WM_DRAWCLIPBOARD:
                        NativeMethods.SendMessage(_fpChainedWindowHandle, m.Msg, m.LParam, m.WParam);
                        _clipboardChangedEvent?.Invoke();
                        break;
                    case NativeMethods.WM_CHANGECBCHAIN:
                        // When a clipboard viewer window receives the WM_CHANGECBCHAIN message,
                        // it should call the SendMessage function to pass the message to the
                        // next window in the chain, unless the next window is the window
                        // being removed. In this case, the clipboard viewer should save
                        // the handle specified by the lParam parameter as the next window in the chain.
                        //
                        // wParam is the Handle to the window being removed from
                        // the clipboard viewer chain
                        // lParam is the Handle to the next window in the chain
                        // following the window being removed.
                        if (m.WParam == _fpChainedWindowHandle) {
                            // If wParam is the next clipboard viewer then it
                            // is being removed so update pointer to the next
                            // window in the clipboard chain
                            _fpChainedWindowHandle = m.LParam;
                        } else {
                            //Send to the next window
                            NativeMethods.SendMessage(_fpChainedWindowHandle, m.Msg, m.LParam, m.WParam);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace("frmMain WndProc failed", ex);
            }

            base.WndProc(ref m);
        }

        private static void SimulateClick(Control control)
        {
            Point clientMousePosition = control.PointToClient(MousePosition);
            int temp_wLow = clientMousePosition.X;
            int temp_wHigh = clientMousePosition.Y;
            NativeMethods.SendMessage(control.Handle, NativeMethods.WM_LBUTTONDOWN, (IntPtr)NativeMethods.MK_LBUTTON,
                                      (IntPtr)NativeMethods.MAKELPARAM(ref temp_wLow, ref temp_wHigh));
            clientMousePosition.X = temp_wLow;
            clientMousePosition.Y = temp_wHigh;
        }

        private void ApplyUnifiedWindowChromeHitTest(ref System.Windows.Forms.Message m)
        {
            if (Fullscreen.Value) return;

            int packedPoint = m.LParam.ToInt32();
            Point screenPoint = new((short)(packedPoint & 0xffff), (short)((packedPoint >> 16) & 0xffff));
            Point clientPoint = PointToClient(screenPoint);

            if (WindowState != FormWindowState.Maximized)
            {
                int borderThickness = Math.Max(6, (int)Math.Ceiling(8 * DeviceDpi / 96f));
                int resizeHitTest = WindowChromeHitTest.ResolveResizeHitTest(ClientSize, clientPoint, borderThickness);
                if (resizeHitTest != WindowChromeHitTest.Client)
                {
                    m.Result = (IntPtr)resizeHitTest;
                    return;
                }
            }

            if (_unifiedWindowHeader.IsCaptionPoint(clientPoint))
                m.Result = (IntPtr)WindowChromeHitTest.Caption;
        }

        private void LayoutUnifiedShell()
        {
            if (_unifiedWindowHeader is null) return;
            tsContainer.Bounds = UnifiedWindowLayout.ContentBounds(ClientSize, _unifiedWindowHeader.Height, _unifiedWindowHeader.Visible);
        }

        private void ConfigureEdgeToEdgeDocking()
        {
            tsContainer.Dock = DockStyle.None;
            tsContainer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tsContainer.Margin = Padding.Empty;
            tsContainer.Padding = Padding.Empty;
            tsContainer.ContentPanel.Margin = Padding.Empty;
            tsContainer.ContentPanel.Padding = Padding.Empty;

            pnlDock.Dock = DockStyle.Fill;
            pnlDock.Margin = Padding.Empty;
            pnlDock.Padding = Padding.Empty;
            pnlDock.Theme.Measures.DockPadding = 0;
        }

        private void ApplyStartupShellLayout()
        {
            pnlDock.DockLeftPortion = AppStartupLayout.SidebarWidthForDpi(DeviceDpi);
            WindowState = AppStartupLayout.ResolveWindowState(
                Properties.OptionsStartupExitPage.Default.StartMinimized,
                Properties.OptionsStartupExitPage.Default.StartFullScreen);
        }

        private void UpdateMaximizedBounds()
        {
            if (!IsHandleCreated) return;
            Screen screen = Screen.FromHandle(Handle);
            MaximizedBounds = WindowMaximizeBounds.Resolve(screen.Bounds, screen.WorkingArea);
        }

        private void ActivateConnection()
        {
            ConnectionWindow cw = pnlDock.ActiveDocument as ConnectionWindow;
            DockPane dp = cw?.ActiveControl as DockPane;

            if (dp?.ActiveContent is not ConnectionTab tab) return;
            InterfaceControl ifc = InterfaceControl.FindInterfaceControl(tab);
            if (ifc == null) return;

            ifc.Protocol.Focus();
            Form conFormWindow = ifc.FindForm();
            ((ConnectionTab)conFormWindow)?.RefreshInterfaceController();
        }

        private void PnlDock_ActiveDocumentChanged(object sender, EventArgs e)
        {
            ActivateConnection();
            sessionsMenu.UpdateMenuState();
        }

        internal void UpdateWindowTitle()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(UpdateWindowTitle));
                return;
            }

            StringBuilder titleBuilder = new(Application.ProductName);
            const string separator = " - ";

            if (Runtime.ConnectionsService.IsConnectionsFileLoaded)
            {
                if (Runtime.ConnectionsService.UsingDatabase)
                {
                    titleBuilder.Append(separator);
                    titleBuilder.Append(Language.SQLServer.TrimEnd(':'));
                }
                else
                {
                    if (!string.IsNullOrEmpty(Runtime.ConnectionsService.ConnectionFileName))
                    {
                        titleBuilder.Append(separator);
                        titleBuilder.Append(Properties.OptionsAppearancePage.Default.ShowCompleteConsPathInTitle ? Runtime.ConnectionsService.ConnectionFileName : Path.GetFileName(Runtime.ConnectionsService.ConnectionFileName));
                    }
                }
            }

            if (!string.IsNullOrEmpty(SelectedConnection?.Name))
            {
                titleBuilder.Append(separator);
                titleBuilder.Append(SelectedConnection.Name);

                if (Properties.Settings.Default.TrackActiveConnectionInConnectionTree)
                    AppWindows.TreeForm.JumpToNode(SelectedConnection);
            }

            Text = titleBuilder.ToString();
        }

        public void ShowHidePanelTabs(DockContent closingDocument = null)
        {
            DocumentStyle newDocumentStyle;

            if (Properties.OptionsTabsPanelsPage.Default.AlwaysShowPanelTabs)
            {
                newDocumentStyle = DocumentStyle.DockingWindow; // Show the panel tabs
            }
            else
            {
                int nonConnectionPanelCount = 0;
                foreach (IDockContent dockContent in pnlDock.Documents)
                {
                    DockContent document = (DockContent)dockContent;
                    if ((closingDocument == null || document != closingDocument) && document is not ConnectionWindow)
                    {
                        nonConnectionPanelCount++;
                    }
                }

                newDocumentStyle = nonConnectionPanelCount == 0
                    ? DocumentStyle.DockingSdi
                    : DocumentStyle.DockingWindow;
            }

            // TODO: See if we can get this to work with DPS
#if false
            foreach (var dockContent in pnlDock.Documents)
			{
				var document = (DockContent)dockContent;
				if (document is ConnectionWindow)
				{
					var connectionWindow = (ConnectionWindow)document;
					if (Settings.Default.AlwaysShowConnectionTabs == false)
					{
						connectionWindow.TabController.HideTabsMode = TabControl.HideTabsModes.HidepnlDock.DockLeftPortion = Always;
					}
					else
					{
						connectionWindow.TabController.HideTabsMode = TabControl.HideTabsModes.ShowAlways;
					}
				}
			}
#endif

            if (pnlDock.DocumentStyle == newDocumentStyle) return;
            pnlDock.DocumentStyle = newDocumentStyle;
            pnlDock.Size = new Size(1, 1);
        }

        public void SetDefaultLayout()
        {
            pnlDock.Visible = false;

            AppWindows.ErrorsForm.Show(pnlDock, DockState.DockLeft);
            AppWindows.ConfigForm.Show(pnlDock, DockState.DockLeft);
            AppWindows.TreeForm.Show(pnlDock, DockState.DockLeft);
            viewMenu._mMenViewErrorsAndInfos.Checked = true;

            ShowFileMenu();

            pnlDock.Visible = true;
        }

        public void ShowFileMenu()
        {
            msMain.Visible = true;
            viewMenu._mMenViewFileMenu.Checked = true;
        }

        public void HideFileMenu()
        {
            // The menu is a permanent part of the custom title bar and cannot be hidden separately.
            ShowFileMenu();
        }

        public void SetLayout()
        {
            pnlDock.Visible = false;

            if (Properties.Settings.Default.ViewMenuMessages == true)
            {
                AppWindows.ErrorsForm.Show(pnlDock, DockState.DockLeft);
                viewMenu._mMenViewErrorsAndInfos.Checked = true;
            }
            else
                viewMenu._mMenViewErrorsAndInfos.Checked = false;


            if (Properties.Settings.Default.ViewMenuExternalTools == true)
            {
                viewMenu.TsExternalTools.Visible = true;
                viewMenu._mMenViewExtAppsToolbar.Checked = true;
            }
            else
            {
                viewMenu.TsExternalTools.Visible = false;
                viewMenu._mMenViewExtAppsToolbar.Checked = false;
            }

            if (Properties.Settings.Default.ViewMenuMultiSSH == true)
            {
                viewMenu.TsMultiSsh.Visible = true;
                viewMenu._mMenViewMultiSshToolbar.Checked = true;
            }
            else
            {
                viewMenu.TsMultiSsh.Visible = false;
                viewMenu._mMenViewMultiSshToolbar.Checked = false;
            }

            if (Properties.Settings.Default.ViewMenuQuickConnect == true)
            {
                viewMenu.TsQuickConnect.Visible = true;
                viewMenu._mMenViewQuickConnectToolbar.Checked = true;
            }
            else
            {
                viewMenu.TsQuickConnect.Visible = false;
                viewMenu._mMenViewQuickConnectToolbar.Checked = false;
            }

            if (Properties.Settings.Default.LockToolbars == true)
            {
                Properties.Settings.Default.LockToolbars = true;
                viewMenu._mMenViewLockToolbars.Checked = true;
            }
            else
            {
                Properties.Settings.Default.LockToolbars = false;
                viewMenu._mMenViewLockToolbars.Checked = false;
            }

            pnlDock.Visible = true;
        }

        public void ShowHideMenu() => _unifiedWindowHeader.Visible = true;

        #endregion

        #region Events

        public delegate void ClipboardchangeEventHandler();

        public static event ClipboardchangeEventHandler ClipboardChanged
        {
            add =>
                _clipboardChangedEvent =
                    (ClipboardchangeEventHandler)Delegate.Combine(_clipboardChangedEvent, value);
            remove =>
                _clipboardChangedEvent =
                    (ClipboardchangeEventHandler)Delegate.Remove(_clipboardChangedEvent, value);
        }

        #endregion

        private void ViewMenu_Opening(object sender, EventArgs e)
        {
            viewMenu.mMenView_DropDownOpening(sender, e);
        }

        private void TsModeUser_Click(object sender, EventArgs e)
        {
            Properties.OptionsRbac.Default.ActiveRole = "UserRole";
        }

        private void TsModeAdmin_Click(object sender, EventArgs e)
        {
            Properties.OptionsRbac.Default.ActiveRole = "AdminRole";
        }
    }
}
