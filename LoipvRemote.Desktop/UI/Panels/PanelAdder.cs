using LoipvRemote.App;
using LoipvRemote.App.Composition;
using LoipvRemote.Messages;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Adapters;
using LoipvRemote.UI.Window;
using System;
using System.Collections;
using System.Linq;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;

namespace LoipvRemote.UI.Panels
{
    [SupportedOSPlatform("windows")]
    public class PanelAdder
    {
        private readonly RuntimeState _runtimeState;
        private readonly MessageCollector _messageCollector;
        private readonly ConnectionWorkspaceAdapter _connectionWorkspace;

        public PanelAdder(
            RuntimeState runtimeState,
            MessageCollector messageCollector,
            ConnectionWorkspaceAdapter connectionWorkspace)
        {
            _runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
            _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
            _connectionWorkspace = connectionWorkspace ?? throw new ArgumentNullException(nameof(connectionWorkspace));
        }

        public IEnumerable<BaseWindow> Panels
        {
            get
            {
                for (int index = 0; index < WindowList.Count; index++)
                    yield return WindowList[index];
            }
        }

        public ConnectionWindow AddPanel(string title = "", bool showImmediately = true)
        {
            try
            {
                ConnectionWindow connectionForm = new(new DockContent());
                BuildConnectionWindowContextMenu(connectionForm);
                SetConnectionWindowTitle(title, connectionForm);
                // Only show immediately if requested (for user-created empty panels)
                // When opening connections, we defer showing until first tab is added
                if (showImmediately)
                    ShowConnectionWindow(connectionForm);
                PrepareTabSupport(connectionForm);
                return connectionForm;
            }
            catch (Exception ex)
            {
                _messageCollector.AddMessage(MessageClass.ErrorMsg, "Couldn\'t add panel" + Environment.NewLine + ex.Message);
                return null;
            }
        }

        public bool DoesPanelExist(string panelName)
        {
            return Panels.OfType<ConnectionWindow>().Any(window => window.TabText == panelName);
        }

        private void ShowConnectionWindow(ConnectionWindow connectionForm)
        {
            _connectionWorkspace.Show(connectionForm);
        }

        private void PrepareTabSupport(ConnectionWindow connectionForm)
        {
            WindowList.Add(connectionForm);
        }

        private static void SetConnectionWindowTitle(string title, ConnectionWindow connectionForm)
        {
            if (string.IsNullOrEmpty(title))
                title = Language.NewPanel;
            connectionForm.SetFormText(title.Replace("&", "&&"));
        }

        private void BuildConnectionWindowContextMenu(DockContent pnlcForm)
        {
            ContextMenuStrip cMen = new();
            ToolStripMenuItem cMenRen = CreateRenameMenuItem(pnlcForm);
            ToolStripMenuItem cMenScreens = CreateScreensMenuItem(pnlcForm);
            ToolStripMenuItem cMenClose = CreateCloseMenuItem(pnlcForm);
            cMen.Items.AddRange(new ToolStripItem[] {cMenRen, cMenScreens, cMenClose});
            pnlcForm.TabPageContextMenuStrip = cMen;
        }

        private ToolStripMenuItem CreateScreensMenuItem(DockContent pnlcForm)
        {
            ToolStripMenuItem cMenScreens = new()
            {
                Text = Language.SendTo,
                Image = Properties.Resources.Monitor_16x,
                Tag = pnlcForm
            };
            cMenScreens.DropDownItems.Add("Dummy");
            cMenScreens.DropDownOpening += cMenConnectionPanelScreens_DropDownOpening;
            return cMenScreens;
        }

        private ToolStripMenuItem CreateRenameMenuItem(DockContent pnlcForm)
        {
            ToolStripMenuItem cMenRen = new()
            {
                Text = Language.Rename,
                Image = Properties.Resources.Rename_16x,
                Tag = pnlcForm
            };
            cMenRen.Click += cMenConnectionPanelRename_Click;
            return cMenRen;
        }

        private ToolStripMenuItem CreateCloseMenuItem(DockContent pnlcForm)
        {
            ToolStripMenuItem cMenClose = new()
            {
                Text = Language._Close,
                Image = Properties.Resources.Close_16x,
                Tag = pnlcForm
            };
            cMenClose.Click += cMenConnectionPanelClose_Click;
            return cMenClose;
        }

        private void cMenConnectionPanelRename_Click(object? sender, EventArgs e)
        {
            try
            {
                ConnectionWindow conW = (ConnectionWindow)((ToolStripMenuItem)sender).Tag;

                using (FrmInputBox newTitle = new(Language.NewTitle, Language.NewTitle + ":", ""))
                    if (newTitle.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(newTitle.returnValue))
                        conW.SetFormText(newTitle.returnValue.Replace("&", "&&"));
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionStackTrace("cMenConnectionPanelRename_Click: Caught Exception: ", ex);
            }
        }

        private void cMenConnectionPanelClose_Click(object? sender, EventArgs e)
        {
            try
            {
                ConnectionWindow conW = (ConnectionWindow)((ToolStripMenuItem)sender).Tag;
                conW.Close();
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionStackTrace("cMenConnectionPanelClose_Click: Caught Exception: ", ex);
            }
        }

        private void cMenConnectionPanelScreens_DropDownOpening(object? sender, EventArgs e)
        {
            try
            {
                ToolStripMenuItem cMenScreens = (ToolStripMenuItem)sender;
                cMenScreens.DropDownItems.Clear();

                for (int i = 0; i <= Screen.AllScreens.Length - 1; i++)
                {
                    ToolStripMenuItem cMenScreen = new(Language.Screen + " " + Convert.ToString(i + 1))
                    {
                        Tag = new ArrayList(),
                        Image = Properties.Resources.Monitor_16x
                    };
                    ((ArrayList)cMenScreen.Tag).Add(Screen.AllScreens[i]);
                    ((ArrayList)cMenScreen.Tag).Add(cMenScreens.Tag);
                    cMenScreen.Click += cMenConnectionPanelScreen_Click;
                    cMenScreens.DropDownItems.Add(cMenScreen);
                }
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionStackTrace("cMenConnectionPanelScreens_DropDownOpening: Caught Exception: ", ex);
            }
        }

        private void cMenConnectionPanelScreen_Click(object? sender, EventArgs e)
        {
            Screen screen = null;
            DockContent panel = null;
            try
            {
                IEnumerable tagEnumeration = (IEnumerable)((ToolStripMenuItem)sender).Tag;
                if (tagEnumeration == null) return;
                foreach (object obj in tagEnumeration)
                {
                    if (obj is Screen screen1)
                    {
                        screen = screen1;
                    }
                    else if (obj is DockContent)
                    {
                        panel = (DockContent)obj;
                    }
                }

                Screens.SendPanelToScreen(panel, screen);
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionStackTrace("cMenConnectionPanelScreen_Click: Caught Exception: ", ex);
            }
        }

        private WindowList WindowList => _runtimeState.WindowList
            ?? throw new InvalidOperationException("Connection windows must be initialized before adding a panel.");
    }
}
