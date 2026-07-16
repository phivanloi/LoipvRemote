using System;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.App;
using LoipvRemote.App.Composition;
using LoipvRemote.Properties;
using LoipvRemote.Resources.Language;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Panels;
using LoipvRemote.UI.Window;

namespace LoipvRemote.UI.Menu
{
    [SupportedOSPlatform("windows")]
    public class ViewMenu : ToolStripMenuItem
    {
        private ToolStripMenuItem _mMenViewConnectionPanels = null!;
        private ToolStripMenuItem _mMenReconnectAll = null!;
        private ToolStripSeparator _mMenViewSep1 = null!;
        public ToolStripMenuItem ViewErrorsAndInfosMenu { get; private set; } = null!;
        public ToolStripMenuItem ViewFileMenu { get; private set; } = null!;
        private ToolStripMenuItem _mMenViewAddConnectionPanel = null!;
        private ToolStripSeparator _mMenViewSep2 = null!;
        private ToolStripMenuItem _mMenViewFullscreen = null!;
        public ToolStripMenuItem ViewExternalAppsToolbar { get; private set; } = null!;
        public ToolStripMenuItem ViewQuickConnectToolbar { get; private set; } = null!;
        public ToolStripMenuItem ViewMultiSshToolbar { get; private set; } = null!;
        private ToolStripMenuItem _mMenViewResetLayout = null!;
        public ToolStripMenuItem ViewLockToolbars { get; private set; } = null!;
        private PanelAdder? _panelAdder;
        private DesktopShellRuntime? _desktopShellRuntime;


        public ToolStrip TsExternalTools { get; set; } = null!;
        public ToolStrip TsQuickConnect { get; set; } = null!;
        public ToolStrip TsMultiSsh { get; set; } = null!;
        public FullscreenHandler FullscreenHandler { get; set; } = null!;
        public FrmMain MainForm { get; set; } = null!;


        public ViewMenu()
        {
            Initialize();
        }

        internal void AttachRuntime(DesktopShellRuntime desktopShellRuntime)
        {
            _desktopShellRuntime = desktopShellRuntime ?? throw new ArgumentNullException(nameof(desktopShellRuntime));
            _panelAdder = desktopShellRuntime.PanelAdder;
        }

        private PanelAdder PanelAdder => _panelAdder
            ?? throw new InvalidOperationException("ViewMenu runtime must be attached before use.");

        private DesktopShellRuntime DesktopShellRuntime => _desktopShellRuntime
            ?? throw new InvalidOperationException("ViewMenu runtime must be attached before use.");

        private void Initialize()
        {
            _mMenViewAddConnectionPanel = new ToolStripMenuItem();
            _mMenViewConnectionPanels = new ToolStripMenuItem();
            _mMenViewSep1 = new ToolStripSeparator();
            ViewFileMenu = new ToolStripMenuItem();
            ViewErrorsAndInfosMenu = new ToolStripMenuItem();
            _mMenViewResetLayout = new ToolStripMenuItem();
            ViewLockToolbars = new ToolStripMenuItem();
            _mMenViewSep2 = new ToolStripSeparator();
            ViewQuickConnectToolbar = new ToolStripMenuItem();
            _mMenReconnectAll = new ToolStripMenuItem();
            ViewExternalAppsToolbar = new ToolStripMenuItem();
            ViewMultiSshToolbar = new ToolStripMenuItem();
            _mMenViewFullscreen = new ToolStripMenuItem();

            //
            // mMenView
            //
            DropDownItems.AddRange(new ToolStripItem[]
            {
                ViewFileMenu,
                ViewErrorsAndInfosMenu,
                ViewQuickConnectToolbar,
                ViewExternalAppsToolbar,
                ViewMultiSshToolbar,
                _mMenViewSep1,
                _mMenReconnectAll,
                _mMenViewAddConnectionPanel,
                _mMenViewConnectionPanels,
                _mMenViewResetLayout,
                ViewLockToolbars,
                _mMenViewSep2,
                _mMenViewFullscreen
            });
            Name = "mMenView";
            Size = new System.Drawing.Size(44, 20);
            Text = Language._View;
            //DropDownOpening += mMenView_DropDownOpening;
            //
            // mMenViewAddConnectionPanel
            //
            _mMenViewAddConnectionPanel.Image = Properties.Resources.InsertPanel_16x;
            _mMenViewAddConnectionPanel.Name = "mMenViewAddConnectionPanel";
            _mMenViewAddConnectionPanel.Size = new System.Drawing.Size(228, 22);
            _mMenViewAddConnectionPanel.Text = Language.AddConnectionPanel;
            _mMenViewAddConnectionPanel.Click += mMenViewAddConnectionPanel_Click;
            //
            // mMenReconnectAll
            //
            _mMenReconnectAll.Image = Properties.Resources.Refresh_16x;
            _mMenReconnectAll.Name = "mMenReconnectAll";
            _mMenReconnectAll.Size = new System.Drawing.Size(281, 22);
            _mMenReconnectAll.Text = Language.ReconnectAllConnections;
            _mMenReconnectAll.Click += mMenReconnectAll_Click;
            //
            // mMenViewConnectionPanels
            //
            _mMenViewConnectionPanels.Image = Properties.Resources.Panel_16x;
            _mMenViewConnectionPanels.Name = "mMenViewConnectionPanels";
            _mMenViewConnectionPanels.Size = new System.Drawing.Size(228, 22);
            _mMenViewConnectionPanels.Text = Language.ConnectionPanels;
            //
            // mMenViewSep1
            //
            _mMenViewSep1.Name = "mMenViewSep1";
            _mMenViewSep1.Size = new System.Drawing.Size(225, 6);
            //
            // mMenViewFile
            //
            ViewFileMenu.Checked = true;
            ViewFileMenu.CheckState = CheckState.Checked;
            ViewFileMenu.Name = "mMenViewFile";
            ViewFileMenu.Size = new System.Drawing.Size(228, 22);
            ViewFileMenu.Text = Language.FileMenu;
            ViewFileMenu.Click += mMenViewFileMenu_Click;
            //
            // mMenViewErrorsAndInfos
            //
            ViewErrorsAndInfosMenu.Checked = true;
            ViewErrorsAndInfosMenu.CheckState = CheckState.Checked;
            ViewErrorsAndInfosMenu.Name = "mMenViewErrorsAndInfos";
            ViewErrorsAndInfosMenu.Size = new System.Drawing.Size(228, 22);
            ViewErrorsAndInfosMenu.Text = Language.Notifications;
            ViewErrorsAndInfosMenu.Click += mMenViewErrorsAndInfos_Click;
            //
            // mMenViewResetLayout
            //
            _mMenViewResetLayout.Name = "mMenViewResetLayout";
            _mMenViewResetLayout.Size = new System.Drawing.Size(228, 22);
            _mMenViewResetLayout.Text = Language.ResetLayout;
            _mMenViewResetLayout.Click += mMenViewResetLayout_Click;
            //
            // mMenViewLockToolbars
            //
            ViewLockToolbars.Name = "mMenViewLockToolbars";
            ViewLockToolbars.Size = new System.Drawing.Size(228, 22);
            ViewLockToolbars.Text = Language.LockToolbars;
            ViewLockToolbars.Click += mMenViewLockToolbars_Click;
            //
            // mMenViewSep2
            //
            _mMenViewSep2.Name = "mMenViewSep2";
            _mMenViewSep2.Size = new System.Drawing.Size(225, 6);
            //
            // mMenViewQuickConnectToolbar
            //
            ViewQuickConnectToolbar.Name = "mMenViewQuickConnectToolbar";
            ViewQuickConnectToolbar.Size = new System.Drawing.Size(228, 22);
            ViewQuickConnectToolbar.Text = Language.QuickConnectToolbar;
            ViewQuickConnectToolbar.Click += mMenViewQuickConnectToolbar_Click;
            //
            // mMenViewExtAppsToolbar
            //
            ViewExternalAppsToolbar.Name = "mMenViewExtAppsToolbar";
            ViewExternalAppsToolbar.Size = new System.Drawing.Size(228, 22);
            ViewExternalAppsToolbar.Text = Language.ExternalToolsToolbar;
            ViewExternalAppsToolbar.Click += mMenViewExtAppsToolbar_Click;
            //
            // mMenViewMultiSSHToolbar
            //
            ViewMultiSshToolbar.Name = "mMenViewMultiSSHToolbar";
            ViewMultiSshToolbar.Size = new System.Drawing.Size(279, 26);
            ViewMultiSshToolbar.Text = Language.MultiSshToolbar;
            ViewMultiSshToolbar.Click += mMenViewMultiSSHToolbar_Click;
            //
            // mMenViewFullscreen
            //
            _mMenViewFullscreen.Image = Properties.Resources.FullScreen_16x;
            _mMenViewFullscreen.Name = "mMenViewFullscreen";
            _mMenViewFullscreen.ShortcutKeys = Keys.F11;
            _mMenViewFullscreen.Size = new System.Drawing.Size(228, 22);
            _mMenViewFullscreen.Text = Language.Fullscreen;
            _mMenViewFullscreen.Checked = Properties.App.Default.MainFormKiosk;
            _mMenViewFullscreen.Click += mMenViewFullscreen_Click;
        }


        public void ApplyLanguage()
        {
            Text = Language._View;
            _mMenViewAddConnectionPanel.Text = Language.AddConnectionPanel;
            _mMenViewConnectionPanels.Text = Language.ConnectionPanels;
            ViewErrorsAndInfosMenu.Text = Language.Notifications;
            _mMenViewResetLayout.Text = Language.ResetLayout;
            ViewLockToolbars.Text = Language.LockToolbars;
            ViewQuickConnectToolbar.Text = Language.QuickConnectToolbar;
            ViewExternalAppsToolbar.Text = Language.ExternalToolsToolbar;
            ViewMultiSshToolbar.Text = Language.MultiSshToolbar;
            _mMenViewFullscreen.Text = Language.Fullscreen;
        }

        #region View

        internal void mMenView_DropDownOpening(object? sender, EventArgs e)
        {
            ViewErrorsAndInfosMenu.Visible = false;
            ViewLockToolbars.Checked = Settings.Default.LockToolbars;

            ViewExternalAppsToolbar.Checked = TsExternalTools.Visible;
            ViewQuickConnectToolbar.Checked = TsQuickConnect.Visible;
            ViewMultiSshToolbar.Checked = TsMultiSsh.Visible;

            _mMenViewConnectionPanels.DropDownItems.Clear();

            foreach (BaseWindow window in PanelAdder.Panels)
            {
                ToolStripMenuItem tItem = new(window.Text, window.Icon?.ToBitmap(), ConnectionPanelMenuItem_Click)
                { Tag = window };
                _mMenViewConnectionPanels.DropDownItems.Add(tItem);
            }

            _mMenViewConnectionPanels.Visible = _mMenViewConnectionPanels.DropDownItems.Count > 0;
        }

        private void ConnectionPanelMenuItem_Click(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem { Tag: BaseWindow window })
                return;

            window.Show(MainForm.pnlDock);
            window.Focus();
        }

        private void mMenViewErrorsAndInfos_Click(object? sender, EventArgs e)
        {
            if (ViewErrorsAndInfosMenu.Checked == false)
            {
                DesktopShellRuntime.Windows.ErrorsForm.Show(MainForm.pnlDock);
                ViewErrorsAndInfosMenu.Checked = true;
            }
            else
            {
                DesktopShellRuntime.Windows.ErrorsForm.Hide();
                ViewErrorsAndInfosMenu.Checked = false;
            }
        }

        private void mMenViewFileMenu_Click(object? sender, EventArgs e)
        {
            if (ViewFileMenu.Checked == false)
            {
                MainForm.ShowFileMenu();
            }
            else
            {
                MainForm.HideFileMenu();
            }
        }

        private void mMenViewResetLayout_Click(object? sender, EventArgs e)
        {
            DialogResult msgBoxResult = MessageBox.Show(Language.ConfirmResetLayout, string.Empty, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (msgBoxResult == DialogResult.Yes)
            {
                MainForm.SetDefaultLayout();
            }
        }

        private void mMenViewLockToolbars_Click(object? sender, EventArgs eventArgs)
        {
            if (ViewLockToolbars.Checked)
            {
                Settings.Default.LockToolbars = false;
                ViewLockToolbars.Checked = false;
            }
            else
            {
                Settings.Default.LockToolbars = true;
                ViewLockToolbars.Checked = true;
            }
        }

        private void mMenViewAddConnectionPanel_Click(object? sender, EventArgs e)
        {
            PanelAdder.AddPanel();
        }

        private void mMenViewExtAppsToolbar_Click(object? sender, EventArgs e)
        {
            if (ViewExternalAppsToolbar.Checked)
            {
                Settings.Default.ViewMenuExternalTools = false;
                ViewExternalAppsToolbar.Checked = false;
                TsExternalTools.Visible = false;
            }
            else
            {
                Settings.Default.ViewMenuExternalTools = true;
                ViewExternalAppsToolbar.Checked = true;
                TsExternalTools.Visible = true;
            }
        }

        private void mMenViewQuickConnectToolbar_Click(object? sender, EventArgs e)
        {
            if (ViewQuickConnectToolbar.Checked)
            {
                Settings.Default.ViewMenuQuickConnect = false;
                ViewQuickConnectToolbar.Checked = false;
                TsQuickConnect.Visible = false;
            }
            else
            {
                Settings.Default.ViewMenuQuickConnect = true;
                ViewQuickConnectToolbar.Checked = true;
                TsQuickConnect.Visible = true;
            }
        }

        private void mMenViewMultiSSHToolbar_Click(object? sender, EventArgs e)
        {
            if (ViewMultiSshToolbar.Checked)
            {
                Settings.Default.ViewMenuMultiSSH = false;
                ViewMultiSshToolbar.Checked = false;
                TsMultiSsh.Visible = false;
            }
            else
            {
                Settings.Default.ViewMenuMultiSSH = true;
                ViewMultiSshToolbar.Checked = true;
                TsMultiSsh.Visible = true;
            }
        }

        private void mMenViewFullscreen_Click(object? sender, EventArgs e)
        {
            FullscreenHandler.Value = !FullscreenHandler.Value;
            _mMenViewFullscreen.Checked = FullscreenHandler.Value;
        }

        private async void mMenReconnectAll_Click(object? sender, EventArgs e)
        {
            foreach (BaseWindow window in PanelAdder.Panels)
            {
                if (!(window is ConnectionWindow connectionWindow))
                    return;

                await connectionWindow.ReconnectAllAsync(DesktopShellRuntime.ConnectionInitiator);
            }
        }

        #endregion
    }
}
