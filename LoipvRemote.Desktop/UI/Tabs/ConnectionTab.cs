using System;
using System.Diagnostics;
using System.Windows.Forms;
using LoipvRemote.App.Info;
using LoipvRemote.Config;
using LoipvRemote.Connection;
using LoipvRemote.Properties;
using LoipvRemote.UI.TaskDialog;
using WeifenLuo.WinFormsUI.Docking;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.UI.Tabs
{
    [SupportedOSPlatform("windows")]
    public partial class ConnectionTab : DockContent
    {
        private bool _protocolFocusScheduled;
        /// <summary>
        ///Silent close ignores the popup asking for confirmation
        /// </summary>
        public bool silentClose { get; set; }

        /// <summary>
        /// Protocol close ignores the interface controller cleanup and the user confirmation dialog
        /// </summary>
        public bool protocolClose { get; set; }

        public ConnectionTab()
        {
            InitializeComponent();
            GotFocus += ConnectionTab_GotFocus;
            Activated += ConnectionTab_Activated;
        }

        private void ConnectionTab_GotFocus(object? sender, EventArgs e)
        {
            TabHelper.Instance.CurrentTab = this;
            ScheduleProtocolFocus();
        }

        private void ConnectionTab_Activated(object? sender, EventArgs e) => ScheduleProtocolFocus();

        private void ScheduleProtocolFocus()
        {
            if (_protocolFocusScheduled || IsDisposed || !IsHandleCreated)
                return;

            _protocolFocusScheduled = true;
            BeginInvoke((MethodInvoker)(() =>
            {
                _protocolFocusScheduled = false;
                if (IsDisposed || !Visible || DockPanel?.ActiveDocument is not ConnectionTab activeTab ||
                    !ReferenceEquals(activeTab, this))
                    return;

                InterfaceControl.FindInterfaceControl(this)?.Protocol.Focus();
            }));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!protocolClose)
            {
                if (!silentClose)
                {
                    if (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseMode.All)
                    {
                        DialogResult result = CTaskDialog.MessageBox(this, GeneralAppInfo.ProductName ?? string.Empty,
                                                            FormatText(Language.ConfirmCloseConnectionPanelMainInstruction,
                                                                        TabText), "", "", "",
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
                            e.Cancel = true;
                        }
                        else
                        {
                            if (Tag is InterfaceControl control)
                                control.Protocol.RequestClose();
                        }
                    }
                    else
                    {
                        // close without the confirmation prompt...
                        if (Tag is InterfaceControl control)
                            control.Protocol.RequestClose();
                    }
                }
                else
                {
                    if (Tag is InterfaceControl control)
                        control.Protocol.RequestClose();
                }
            }

            base.OnFormClosing(e);
        }


        #region HelperFunctions

        public void RefreshInterfaceController()
        {
            try
            {
                InterfaceControl? interfaceControl = Tag as InterfaceControl;
                if (interfaceControl?.Protocol is IRemoteScreenController screen)
                    screen.RefreshScreen();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"RefreshIC (UI.Window.Connection) failed.{Environment.NewLine}{ex}");
            }
        }

        #endregion
    }
}
