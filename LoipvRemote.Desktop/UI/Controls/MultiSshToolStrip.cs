using System.ComponentModel;
using System.Windows.Forms;
using LoipvRemote.Themes;
using System;
using System.Collections;
using System.Linq;
using LoipvRemote.App;
using LoipvRemote.App.Composition;
using LoipvRemote.Connection;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;

namespace LoipvRemote.UI.Controls
{
    [SupportedOSPlatform("windows")]
    public partial class MultiSshToolStrip : ToolStrip
    {
        private System.ComponentModel.Container? components;
        private ToolStripLabel lblMultiSsh = null!;
        private ToolStripTextBox txtMultiSsh = null!;
        private int previousCommandIndex;
        private readonly List<IInputMessageTarget> processHandlers = [];
        private readonly ArrayList quickConnectConnections = [];
        private readonly ArrayList previousCommands = [];
        private readonly ThemeManager _themeManager;
        private DesktopShellRuntime? _desktopShellRuntime;

        private int CommandHistoryLength { get; set; } = 100;

        public MultiSshToolStrip()
        {
            InitializeComponent();
            _themeManager = ThemeManager.getInstance();
            _themeManager.ThemeChanged += ApplyTheme;
            ApplyTheme();
        }

        internal void AttachRuntime(DesktopShellRuntime desktopShellRuntime) =>
            _desktopShellRuntime = desktopShellRuntime ?? throw new ArgumentNullException(nameof(desktopShellRuntime));

        private DesktopShellRuntime DesktopShellRuntime => _desktopShellRuntime
            ?? throw new InvalidOperationException("The desktop shell runtime must be attached before using multi SSH.");

        private void ApplyTheme()
        {
            if (!_themeManager.ActiveAndExtended) return;
            txtMultiSsh.BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Background");
            txtMultiSsh.ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Foreground");
        }

        private static List<IInputMessageTarget> ProcessOpenConnections(ConnectionInfo connection)
        {
            List<IInputMessageTarget> handlers = [];

            foreach (IProtocolSession protocol in connection.OpenConnections)
            {
                if (protocol.Capabilities.HasFlag(ProtocolCapabilities.InputForwarding) &&
                    protocol is IInputMessageTarget target)
                {
                    handlers.Add(target);
                }
            }

            return handlers;
        }

        private void SendAllKeystrokes(int keyType, int keyData)
        {
            if (processHandlers.Count == 0) return;

            foreach (IInputMessageTarget protocol in processHandlers)
            {
                protocol.TryForwardInputMessage(keyType, new IntPtr(keyData), IntPtr.Zero);
            }
        }

        #region Key Event Handler

        private void RefreshActiveConnections(object? sender, EventArgs e)
        {
            processHandlers.Clear();
            foreach (ConnectionInfo connection in quickConnectConnections)
            {
                processHandlers.AddRange(ProcessOpenConnections(connection));
            }

            System.Collections.Generic.IEnumerable<ConnectionInfo> connectionTreeConnections = DesktopShellRuntime.ConnectionTreeWorkspace.ConnectionTreeModel.GetRecursiveChildList().Where(item => item.OpenConnections.Count > 0);

            foreach (ConnectionInfo connection in connectionTreeConnections)
            {
                processHandlers.AddRange(ProcessOpenConnections(connection));
            }
        }

        private void ProcessKeyPress(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                try
                {
                    switch (e.KeyCode)
                    {
                        case Keys.Up when previousCommandIndex - 1 >= 0:
                            previousCommandIndex -= 1;
                            break;
                        case Keys.Down when previousCommandIndex + 1 < previousCommands.Count:
                            previousCommandIndex += 1;
                            break;
                        default:
                            return;
                    }
                }
                catch { }

                txtMultiSsh.Text = previousCommands[previousCommandIndex]?.ToString() ?? string.Empty;
                txtMultiSsh.SelectAll();
            }

            if (e.Control && e.KeyCode != Keys.V && e.Alt == false)
            {
                SendAllKeystrokes(InputWindowMessages.KeyDown, e.KeyValue);
            }

            if (e.KeyCode == Keys.Enter)
            {
                foreach (char chr1 in txtMultiSsh.Text)
                {
                    SendAllKeystrokes(InputWindowMessages.Character, Convert.ToByte(chr1));
                }

                SendAllKeystrokes(InputWindowMessages.KeyDown, 13); // Enter = char13
            }
        }

        private void ProcessKeyRelease(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            if (string.IsNullOrWhiteSpace(txtMultiSsh.Text)) return;

            previousCommands.Add(txtMultiSsh.Text.Trim());

            if (previousCommands.Count >= CommandHistoryLength) previousCommands.RemoveAt(0);

            previousCommandIndex = previousCommands.Count - 1;
            txtMultiSsh.Clear();
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                    components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblMultiSsh = new ToolStripLabel();
            this.txtMultiSsh = new ToolStripTextBox();
            this.SuspendLayout();
            //
            // lblMultiSSH
            //
            this.lblMultiSsh.Name = "_lblMultiSsh";
            this.lblMultiSsh.Size = new System.Drawing.Size(77, 22);
            this.lblMultiSsh.Text = Language.MultiSsh;
            //
            // txtMultiSsh
            //
            this.txtMultiSsh.Name = "_txtMultiSsh";
            this.txtMultiSsh.Size = new System.Drawing.Size(new DisplayProperties().ScaleWidth(300), 25);
            this.txtMultiSsh.ToolTipText = Language.MultiSshToolTip;
            this.txtMultiSsh.Enter += RefreshActiveConnections;
            this.txtMultiSsh.KeyDown += ProcessKeyPress;
            this.txtMultiSsh.KeyUp += ProcessKeyRelease;

            this.Items.AddRange(new ToolStripItem[]
            {
                lblMultiSsh,
                txtMultiSsh
            });
            this.ResumeLayout(false);
        }

        #endregion

    }
}
