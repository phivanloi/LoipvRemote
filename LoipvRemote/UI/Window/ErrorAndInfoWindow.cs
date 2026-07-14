using System;
using System.Drawing;
using System.Collections;
using System.Globalization;
using System.Windows.Forms;
using System.Text;
using WeifenLuo.WinFormsUI.Docking;
using LoipvRemote.App;
using LoipvRemote.Messages;
using LoipvRemote.UI.Forms;
using LoipvRemote.Themes;
using LoipvRemote.Resources.Language;
using LoipvRemote.UI.Adapters;
using Message = LoipvRemote.Messages.Message;
using System.Runtime.Versioning;

namespace LoipvRemote.UI.Window
{
    [SupportedOSPlatform("windows")]
    public partial class ErrorAndInfoWindow : BaseWindow
    {
        private readonly ThemeManager _themeManager;
        private readonly DisplayProperties _display;
        private MessageCollector? _messageCollector;
        private ConnectionWorkspaceAdapter? _connectionWorkspace;

        private MessageCollector MessageCollector => _messageCollector
            ?? throw new InvalidOperationException("ErrorAndInfoWindow services must be attached before use.");

        private ConnectionWorkspaceAdapter ConnectionWorkspace => _connectionWorkspace
            ?? throw new InvalidOperationException("ErrorAndInfoWindow services must be attached before use.");

        public DockContent PreviousActiveForm { get; set; }

        public ErrorAndInfoWindow() : this(new DockContent())
        {
        }

        public ErrorAndInfoWindow(DockContent panel)
        {
            WindowType = WindowType.ErrorsAndInfos;
            DockPnl = panel;
            _display = new DisplayProperties();
            InitializeComponent();
            Icon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.StatusInformation_16x);
            lblMsgDate.Width = _display.ScaleWidth(lblMsgDate.Width);
            _themeManager = ThemeManager.getInstance();
            ApplyTheme();
            _themeManager.ThemeChanged += ApplyTheme;
            UpdateNotificationLayout();
            FillImageList();
            ApplyLanguage();
        }

        public void AttachServices(MessageCollector messageCollector, ConnectionWorkspaceAdapter connectionWorkspace)
        {
            _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
            _connectionWorkspace = connectionWorkspace ?? throw new ArgumentNullException(nameof(connectionWorkspace));
        }

        #region Form Stuff

        private void ErrorsAndInfos_Load(object sender, EventArgs e)
        {
        }

        private void ApplyLanguage()
        {
            clmMessage.Text = Language.Message;
            cMenMCCopy.Text = Language.CopyAll;
            cMenMCDelete.Text = Language.DeleteAll;
            TabText = Language.Notifications;
            Text = Language.Notifications;
            lblEmptyNotifications.Text = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi"
                ? "Không có thông báo"
                : "No notifications";
        }

        #endregion

        #region Private Methods

        private new void ApplyTheme()
        {
            if (!_themeManager.ActiveAndExtended) return;
            lvErrorCollector.BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Background");
            lvErrorCollector.ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Foreground");
            lblEmptyNotifications.BackColor = lvErrorCollector.BackColor;
            lblEmptyNotifications.ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Foreground");

            pnlErrorMsg.BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
            pnlErrorMsg.ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");
            txtMsgText.BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Background");
            txtMsgText.ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Foreground");
            lblMsgDate.BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
            lblMsgDate.ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");
        }

        private void FillImageList()
        {
            imgListMC.ImageSize = _display.ScaleSize(imgListMC.ImageSize);
            imgListMC.Images.Add(_display.ScaleImage(Properties.Resources.Test_16x));
            imgListMC.Images.Add(_display.ScaleImage(Properties.Resources.StatusInformation_16x));
            imgListMC.Images.Add(_display.ScaleImage(Properties.Resources.LogWarning_16x));
            imgListMC.Images.Add(_display.ScaleImage(Properties.Resources.LogError_16x));
        }

        private void LayoutVertical()
        {
            try
            {
                int clientWidth = ClientSize.Width;
                int clientHeight = ClientSize.Height;
                int gap = _display.ScaleHeight(5);
                int detailHeight = Math.Min(_display.ScaleHeight(200), Math.Max(_display.ScaleHeight(120), clientHeight / 3));
                detailHeight = Math.Min(detailHeight, Math.Max(0, clientHeight - gap));

                pnlErrorMsg.Location = new Point(0, clientHeight - detailHeight);
                pnlErrorMsg.Size = new Size(clientWidth, detailHeight);
                pnlErrorMsg.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                txtMsgText.Size = new Size(
                                           Math.Max(0, pnlErrorMsg.Width - pbError.Width - _display.ScaleWidth(8)),
                                           Math.Max(0, pnlErrorMsg.Height - _display.ScaleHeight(20)));
                lvErrorCollector.Location = new Point(0, 0);
                lvErrorCollector.Size = new Size(clientWidth, Math.Max(0, clientHeight - detailHeight - gap));
                lvErrorCollector.Anchor =
                    AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg,
                                                    "LayoutVertical (UI.Window.ErrorsAndInfos) failed" +
                                                    Environment.NewLine + ex.Message, true);
            }
        }

        private void LayoutHorizontal()
        {
            try
            {
                int clientWidth = ClientSize.Width;
                int clientHeight = ClientSize.Height;
                int gap = _display.ScaleWidth(5);
                int detailWidth = Math.Min(_display.ScaleWidth(320), Math.Max(_display.ScaleWidth(220), clientWidth / 3));
                detailWidth = Math.Min(detailWidth, Math.Max(0, clientWidth - gap));

                pnlErrorMsg.Location = new Point(0, 0);
                pnlErrorMsg.Size = new Size(detailWidth, clientHeight);
                pnlErrorMsg.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Top;

                txtMsgText.Size = new Size(
                                           Math.Max(0, pnlErrorMsg.Width - pbError.Width - _display.ScaleWidth(8)),
                                           Math.Max(0, pnlErrorMsg.Height - _display.ScaleHeight(20)));
                lvErrorCollector.Location = new Point(detailWidth + gap, 0);
                lvErrorCollector.Size = new Size(Math.Max(0, clientWidth - detailWidth - gap), clientHeight);
                lvErrorCollector.Anchor =
                    AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg,
                                                    "LayoutHorizontal (UI.Window.ErrorsAndInfos) failed" +
                                                    Environment.NewLine + ex.Message, true);
            }
        }

        private void ErrorsAndInfos_Resize(object sender, EventArgs e)
        {
            try
            {
                UpdateNotificationLayout();
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg,
                                                    "ErrorsAndInfos_Resize (UI.Window.ErrorsAndInfos) failed" +
                                                    Environment.NewLine + ex.Message, true);
            }
        }

        internal void UpdateNotificationLayout()
        {
            bool showDetails = lvErrorCollector.SelectedItems.Count == 1;
            bool showEmptyState = lvErrorCollector.Items.Count == 0;
            SuspendLayout();
            try
            {
                lblEmptyNotifications.Visible = showEmptyState;
                pnlErrorMsg.Visible = showDetails;
                if (!showDetails)
                {
                    lvErrorCollector.Anchor = AnchorStyles.None;
                    lvErrorCollector.Dock = DockStyle.Fill;
                    lvErrorCollector.Bounds = ClientRectangle;
                }
                else
                {
                    lvErrorCollector.Dock = DockStyle.None;
                    txtMsgText.Visible = true;
                    pbError.Visible = true;
                    if (ClientSize.Width > ClientSize.Height)
                        LayoutHorizontal();
                    else
                        LayoutVertical();
                }

                if (lvErrorCollector.Columns.Count > 0)
                    lvErrorCollector.Columns[0].Width = Math.Max(0, lvErrorCollector.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);

                if (showEmptyState)
                    lblEmptyNotifications.BringToFront();
                else if (showDetails)
                    pnlErrorMsg.BringToFront();
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        private void SetStyleWhenNoMessageSelected()
        {
            try
            {
                pnlErrorMsg.BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
                pbError.Image = null;
                txtMsgText.BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Background");
                txtMsgText.Text = "";
                lblMsgDate.BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
                lblMsgDate.Text = "";
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg,
                                                    "pnlErrorMsg_ResetDefaultStyle (UI.Window.ErrorsAndInfos) failed" +
                                                    Environment.NewLine +
                                                    ex.Message, true);
            }
        }

        private void MC_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode != Keys.Escape) return;
                try
                {
                    if (PreviousActiveForm != null)
                        PreviousActiveForm.Show(ConnectionWorkspace.MainWindow.pnlDock);
                    else
                        ConnectionWorkspace.Show(AppWindows.TreeForm);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg,
                                                    "MC_KeyDown (UI.Window.ErrorsAndInfos) failed" +
                                                    Environment.NewLine + ex.Message, true);
            }
        }

        private void lvErrorCollector_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (lvErrorCollector.SelectedItems.Count == 0 | lvErrorCollector.SelectedItems.Count > 1)
                {
                    SetStyleWhenNoMessageSelected();
                    UpdateNotificationLayout();
                    return;
                }

                UpdateNotificationLayout();

                ListViewItem sItem = lvErrorCollector.SelectedItems[0];
                Message eMsg = (Message)sItem.Tag;
                switch (eMsg.Class)
                {
                    case MessageClass.DebugMsg:
                        pbError.Image = _display.ScaleImage(Properties.Resources.Test_16x);
                        if (_themeManager.ThemingActive)
                        {
                            pnlErrorMsg.BackColor = Color.LightSteelBlue;
                            txtMsgText.BackColor = Color.LightSteelBlue;
                            lblMsgDate.BackColor = Color.LightSteelBlue;
                        }

                        break;
                    case MessageClass.InformationMsg:
                        pbError.Image = _display.ScaleImage(Properties.Resources.StatusInformation_16x);
                        if (_themeManager.ThemingActive)
                        {
                            pnlErrorMsg.BackColor = Color.LightSteelBlue;
                            txtMsgText.BackColor = Color.LightSteelBlue;
                            lblMsgDate.BackColor = Color.LightSteelBlue;
                        }

                        break;
                    case MessageClass.WarningMsg:
                        pbError.Image = _display.ScaleImage(Properties.Resources.LogWarning_16x);
                        if (_themeManager.ActiveAndExtended)
                        {
                            //Inverse colors for dramatic effect
                            pnlErrorMsg.BackColor =
                                _themeManager.ActiveTheme.ExtendedPalette.getColor("WarningText_Foreground");
                            pnlErrorMsg.ForeColor =
                                _themeManager.ActiveTheme.ExtendedPalette.getColor("WarningText_Background");
                            txtMsgText.BackColor =
                                _themeManager.ActiveTheme.ExtendedPalette.getColor("WarningText_Foreground");
                            txtMsgText.ForeColor =
                                _themeManager.ActiveTheme.ExtendedPalette.getColor("WarningText_Background");
                            lblMsgDate.BackColor =
                                _themeManager.ActiveTheme.ExtendedPalette.getColor("WarningText_Foreground");
                            lblMsgDate.ForeColor =
                                _themeManager.ActiveTheme.ExtendedPalette.getColor("WarningText_Background");
                        }

                        break;
                    case MessageClass.ErrorMsg:
                        pbError.Image = _display.ScaleImage(Properties.Resources.LogError_16x);
                        if (_themeManager.ActiveAndExtended)
                        {
                            pnlErrorMsg.BackColor =
                                _themeManager.ActiveTheme.ExtendedPalette.getColor("ErrorText_Foreground");
                            pnlErrorMsg.ForeColor =
                                _themeManager.ActiveTheme.ExtendedPalette.getColor("ErrorText_Background");
                            txtMsgText.BackColor =
                                _themeManager.ActiveTheme.ExtendedPalette.getColor("ErrorText_Foreground");
                            txtMsgText.ForeColor =
                                _themeManager.ActiveTheme.ExtendedPalette.getColor("ErrorText_Background");
                            lblMsgDate.BackColor =
                                _themeManager.ActiveTheme.ExtendedPalette.getColor("ErrorText_Foreground");
                            lblMsgDate.ForeColor =
                                _themeManager.ActiveTheme.ExtendedPalette.getColor("ErrorText_Background");
                        }

                        break;
                }

                lblMsgDate.Text = eMsg.Date.ToString(CultureInfo.InvariantCulture);
                txtMsgText.Text = eMsg.Text;
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg,
                                                    "lvErrorCollector_SelectedIndexChanged (UI.Window.ErrorsAndInfos) failed" +
                                                    Environment.NewLine +
                                                    ex.Message, true);
            }
        }

        private void cMenMC_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (lvErrorCollector.Items.Count > 0)
            {
                cMenMCCopy.Enabled = true;
                cMenMCDelete.Enabled = true;
                pbError.Visible = true;
            }
            else
            {
                cMenMCCopy.Enabled = false;
                cMenMCDelete.Enabled = false;
            }

            if (lvErrorCollector.SelectedItems.Count > 0)
            {
                cMenMCCopy.Text = Language.Copy;
                cMenMCDelete.Text = Language.Delete;
            }
            else
            {
                cMenMCCopy.Text = Language.CopyAll;
                cMenMCDelete.Text = Language.DeleteAll;
            }
        }

        private void cMenMCCopy_Click(object sender, EventArgs e)
        {
            CopyMessagesToClipboard();
        }

        private void CopyMessagesToClipboard()
        {
            try
            {
                IEnumerable items;
                if (lvErrorCollector.SelectedItems.Count > 0)
                {
                    items = lvErrorCollector.SelectedItems;
                }
                else
                {
                    items = lvErrorCollector.Items;
                }

                StringBuilder stringBuilder = new();
                stringBuilder.AppendLine("----------");

                lvErrorCollector.BeginUpdate();

                foreach (ListViewItem item in items)
                {
                    if (!(item.Tag is Message message))
                    {
                        continue;
                    }

                    stringBuilder.AppendLine(message.Class.ToString());
                    stringBuilder.AppendLine(message.Date.ToString(CultureInfo.InvariantCulture));
                    stringBuilder.AppendLine(message.Text);
                    stringBuilder.AppendLine("----------");
                }

                Clipboard.SetText(stringBuilder.ToString());
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg,
                                                    "UI.Window.ErrorsAndInfos.CopyMessagesToClipboard() failed." +
                                                    Environment.NewLine + ex.Message,
                                                    true);
            }
            finally
            {
                lvErrorCollector.EndUpdate();
            }
        }

        private void cMenMCDelete_Click(object sender, EventArgs e)
        {
            DeleteMessages();
        }

        private void DeleteMessages()
        {
            try
            {
                lvErrorCollector.BeginUpdate();

                if (lvErrorCollector.SelectedItems.Count > 0)
                {
                    foreach (ListViewItem item in lvErrorCollector.SelectedItems)
                        item.Remove();
                }
                else
                {
                    lvErrorCollector.Items.Clear();
                }

                if (lvErrorCollector.Items.Count == 0)
                {
                    pbError.Visible = false;
                    txtMsgText.Visible = false;
                }
                UpdateNotificationLayout();
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg,
                                                    "UI.Window.ErrorsAndInfos.DeleteMessages() failed" +
                                                    Environment.NewLine + ex.Message, true);
            }
            finally
            {
                lvErrorCollector.EndUpdate();
            }
        }

        #endregion

    }
}
