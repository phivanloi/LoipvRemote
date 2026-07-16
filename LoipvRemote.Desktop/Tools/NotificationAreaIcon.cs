using System;
using System.Linq;
using System.Windows.Forms;
using LoipvRemote.App;
using LoipvRemote.App.Composition;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Properties;
using LoipvRemote.UI.Forms;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;


namespace LoipvRemote.Tools
{
    [SupportedOSPlatform("windows")]
    public class NotificationAreaIcon : IDisposable
    {
        private readonly NotifyIcon _nI = null!;
        private readonly ContextMenuStrip _cMen = null!;
        private readonly ToolStripMenuItem _cMenCons = null!;
        private readonly FrmMain _mainForm;
        private readonly DesktopShellRuntime _desktopShellRuntime;

        public bool Disposed { get; private set; }

        public NotificationAreaIcon(FrmMain mainForm, DesktopShellRuntime desktopShellRuntime)
        {
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _desktopShellRuntime = desktopShellRuntime ?? throw new ArgumentNullException(nameof(desktopShellRuntime));
            try
            {
                _cMenCons = new ToolStripMenuItem
                {
                    Text = Language.Connections,
                    Image = Properties.Resources.ASPWebSite_16x
                };

                ToolStripSeparator cMenSep1 = new();

                ToolStripMenuItem cMenExit = new() { Text = Language.Exit };
                cMenExit.Click += cMenExit_Click;

                _cMen = new ContextMenuStrip
                {
                    Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular,
                                                   System.Drawing.GraphicsUnit.Point, Convert.ToByte(0)),
                    RenderMode = ToolStripRenderMode.Professional
                };
                _cMen.Items.AddRange(new ToolStripItem[] { _cMenCons, cMenSep1, cMenExit });

                _nI = new NotifyIcon
                {
                    Text = @"LoipvRemote",
                    BalloonTipText = @"LoipvRemote",
                    Icon = Properties.Resources.LoipvRemote_Icon,
                    ContextMenuStrip = _cMen,
                    Visible = true
                };

                _nI.MouseClick += nI_MouseClick;
                _nI.MouseDoubleClick += nI_MouseDoubleClick;
            }
            catch (Exception ex)
            {
                _desktopShellRuntime.MessageCollector.AddExceptionStackTrace("Creating new SysTrayIcon failed", ex);
            }
        }

        public void Dispose()
        {
            if (Disposed)
                return;

            try
            {
                _nI.MouseClick -= nI_MouseClick;
                _nI.MouseDoubleClick -= nI_MouseDoubleClick;
                _nI.Visible = false;
                _nI.Dispose();
                _cMen.Dispose();
                Disposed = true;
                GC.SuppressFinalize(this);
            }
            catch (Exception ex)
            {
                _desktopShellRuntime.MessageCollector.AddExceptionStackTrace("Disposing SysTrayIcon failed", ex);
            }
        }

        private void nI_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            _cMenCons.DropDownItems.Clear();
            ConnectionsTreeToMenuItemsConverter menuItemsConverter = new(_desktopShellRuntime.MessageCollector)
            {
                MouseUpEventHandler = ConMenItem_MouseUp
            };

            // ReSharper disable once CoVariantArrayConversion
            ToolStripItem[] rootMenuItems = menuItemsConverter
                                            .CreateToolStripDropDownItems(_desktopShellRuntime.ConnectionTreeWorkspace
                                                                                 .ConnectionTreeModel).ToArray();
            _cMenCons.DropDownItems.AddRange(rootMenuItems);
        }

        private void nI_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (_mainForm.Visible)
            {
                HideForm();
                _mainForm.ShowInTaskbar = false;
            }
            else
            {
                ShowForm();
                _mainForm.ShowInTaskbar = true;
            }
        }

        private void ShowForm()
        {
            _mainForm.Show();
            _mainForm.WindowState = _mainForm.PreviousWindowState;

            if (Properties.OptionsAppearancePage.Default.ShowSystemTrayIcon) return;
            _desktopShellRuntime.RuntimeState.NotificationAreaIcon?.Dispose();
            _desktopShellRuntime.RuntimeState.NotificationAreaIcon = null;
        }

        private void HideForm()
        {
            _mainForm.Hide();
            _mainForm.PreviousWindowState = _mainForm.WindowState;
        }

        private void ConMenItem_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (sender is not ToolStripMenuItem menuItem || menuItem.Tag is ContainerInfo) return;
            if (_mainForm.Visible == false)
                ShowForm();
            if (menuItem.Tag is ConnectionInfo connectionInfo)
                _ = _desktopShellRuntime.ConnectionInitiator.OpenConnectionAsync(connectionInfo);
        }

        private static void cMenExit_Click(object? sender, EventArgs e)
        {
            Shutdown.Quit();
        }
    }
}
