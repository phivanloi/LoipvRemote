using System;
using LoipvRemote.App;
using LoipvRemote.Config;
using LoipvRemote.Config.Connections;
using LoipvRemote.Properties;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;
using LoipvRemote.Config.Settings.Registry;

namespace LoipvRemote.UI.Forms.OptionsPages
{
    [SupportedOSPlatform("windows")]
    public sealed partial class ConnectionsPage
    {
        #region Private Fields
        private OptRegistryConnectionsPage pageRegSettingsInstance = null!;
        private readonly FrmMain _frmMain = FrmMain.Default;

        #endregion

        public ConnectionsPage()
        {
            InitializeComponent();
            ApplyTheme();
            PageIcon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.ASPWebSite_16x);

            // Reload settings when page becomes visible to reflect any changes made outside the Options dialog
            VisibleChanged += ConnectionsPage_VisibleChanged;
        }

        private void ConnectionsPage_VisibleChanged(object? sender, EventArgs e)
        {
            if (Visible)
            {
                LoadSettings();
            }
        }

        public override string PageName
        {
            get => Language.Connections;
            set { }
        }

        public override void ApplyLanguage()
        {
            base.ApplyLanguage();

            chkSingleClickOnConnectionOpensIt.Text = Language.SingleClickOnConnectionOpensIt;
            chkSingleClickOnOpenedConnectionSwitchesToIt.Text = Language.SingleClickOnOpenConnectionSwitchesToIt;
            chkConnectionTreeTrackActiveConnection.Text = Language.TrackActiveConnectionInConnectionTree;
            chkHostnameLikeDisplayName.Text = Language.SetHostnameLikeDisplayName;
            chkDoNotTrimUsername.Text = Language.DoNotTrimUsername;
            chkSlowClickRename.Text = Language.SlowClickRenameEnabled;
            chkOpenMultipleConnectionsWithEnter.Text = Language.OpenAllSelectedConnectionsWithEnter;

            lblRdpReconnectionCount.Text = Language.RdpReconnectCount;
            lblRDPConTimeout.Text = Language.RdpOverallConnectionTimeout;
            lblAutoSave1.Text = Language.AutoSaveEvery;

            lblClosingConnections.Text = Language.ClosingConnections;
            radCloseWarnAll.Text = Language._CloseWarnAll;
            radCloseWarnMultiple.Text = Language.RadioCloseWarnMultiple;
            radCloseWarnExit.Text = Language.RadioCloseWarnExit;
            radCloseWarnNever.Text = Language.RadioCloseWarnNever;

            lblRegistrySettingsUsedInfo.Text = Language.OptionsCompanyPolicyMessage;
        }

        public override void LoadSettings()
        {
            chkSingleClickOnConnectionOpensIt.Checked = Settings.Default.SingleClickOnConnectionOpensIt;
            chkSingleClickOnOpenedConnectionSwitchesToIt.Checked = Settings.Default.SingleClickSwitchesToOpenConnection;
            chkConnectionTreeTrackActiveConnection.Checked = Settings.Default.TrackActiveConnectionInConnectionTree;
            chkHostnameLikeDisplayName.Checked = Settings.Default.SetHostnameLikeDisplayName;

            chkDoNotTrimUsername.Checked = Settings.Default.DoNotTrimUsername;
            chkSlowClickRename.Checked = Settings.Default.SlowClickRenameEnabled;
            chkOpenMultipleConnectionsWithEnter.Checked = Settings.Default.OpenMultipleConnectionsWithEnter;

            numRdpReconnectionCount.Value = Convert.ToDecimal(Settings.Default.RdpReconnectionCount);
            numRDPConTimeout.Value = Convert.ToDecimal(Settings.Default.ConRDPOverallConnectionTimeout);
            numAutoSave.Value = Convert.ToDecimal(Properties.OptionsBackupPage.Default.AutoSaveEveryMinutes);

            // Load ConfirmCloseConnection setting
            switch (Settings.Default.ConfirmCloseConnection)
            {
                case (int)ConfirmCloseMode.Never:
                    radCloseWarnNever.Checked = true;
                    break;
                case (int)ConfirmCloseMode.Exit:
                    radCloseWarnExit.Checked = true;
                    break;
                case (int)ConfirmCloseMode.Multiple:
                    radCloseWarnMultiple.Checked = true;
                    break;
                case (int)ConfirmCloseMode.All:
                    radCloseWarnAll.Checked = true;
                    break;
                default:
                    radCloseWarnAll.Checked = true;
                    break;
            }

            if (Properties.OptionsBackupPage.Default.SaveConnectionsFrequency == (int)ConnectionsBackupFrequency.Unassigned)
            {
                if (Properties.OptionsBackupPage.Default.SaveConsOnExit)
                {
                    Properties.OptionsBackupPage.Default.SaveConnectionsFrequency = (int)ConnectionsBackupFrequency.OnExit;
                }
                else
                {
                    Properties.OptionsBackupPage.Default.SaveConnectionsFrequency = (int)ConnectionsBackupFrequency.Never;
                }
            }
        }

        public override void SaveSettings()
        {
            Properties.Settings.Default.SingleClickOnConnectionOpensIt = chkSingleClickOnConnectionOpensIt.Checked;
            Properties.Settings.Default.SingleClickSwitchesToOpenConnection = chkSingleClickOnOpenedConnectionSwitchesToIt.Checked;
            Properties.Settings.Default.TrackActiveConnectionInConnectionTree = chkConnectionTreeTrackActiveConnection.Checked;
            Properties.Settings.Default.SetHostnameLikeDisplayName = chkHostnameLikeDisplayName.Checked;

            Properties.Settings.Default.DoNotTrimUsername = chkDoNotTrimUsername.Checked;
            Properties.Settings.Default.SlowClickRenameEnabled = chkSlowClickRename.Checked;
            Properties.Settings.Default.OpenMultipleConnectionsWithEnter = chkOpenMultipleConnectionsWithEnter.Checked;

            Properties.Settings.Default.RdpReconnectionCount = (int)numRdpReconnectionCount.Value;
            Properties.Settings.Default.ConRDPOverallConnectionTimeout = (int)numRDPConTimeout.Value;
            Properties.OptionsBackupPage.Default.AutoSaveEveryMinutes = (int)numAutoSave.Value;
            if (Properties.OptionsBackupPage.Default.AutoSaveEveryMinutes > 0)
            {
                _frmMain.tmrAutoSave.Interval = Properties.OptionsBackupPage.Default.AutoSaveEveryMinutes * 60000;
                _frmMain.tmrAutoSave.Enabled = true;
            }
            else
            {
                _frmMain.tmrAutoSave.Enabled = false;
            }

            // Save ConfirmCloseConnection setting
            if (radCloseWarnNever.Checked)
            {
                Settings.Default.ConfirmCloseConnection = (int)ConfirmCloseMode.Never;
            }
            else if (radCloseWarnExit.Checked)
            {
                Settings.Default.ConfirmCloseConnection = (int)ConfirmCloseMode.Exit;
            }
            else if (radCloseWarnMultiple.Checked)
            {
                Settings.Default.ConfirmCloseConnection = (int)ConfirmCloseMode.Multiple;
            }
            else if (radCloseWarnAll.Checked)
            {
                Settings.Default.ConfirmCloseConnection = (int)ConfirmCloseMode.All;
            }
        }

        public override void LoadRegistrySettings()
        {
            Type settingsType = typeof(OptRegistryConnectionsPage);
            RegistryLoader.RegistrySettings.TryGetValue(settingsType, out var settings);
            pageRegSettingsInstance = settings as OptRegistryConnectionsPage ?? new OptRegistryConnectionsPage();

            // If registry settings don't exist, create a default instance to prevent null reference exceptions
            if (pageRegSettingsInstance == null)
            {
                pageRegSettingsInstance = new OptRegistryConnectionsPage();
                Logger.Instance.Log?.Debug("[ConnectionsPage.LoadRegistrySettings] pageRegSettingsInstance was null, created default instance");
            }

            RegistryLoader.Cleanup(settingsType);

            // ***
            // Disable controls based on the registry settings.
            //
            if (pageRegSettingsInstance.SingleClickOnConnectionOpensIt.IsSet)
                DisableControl(chkSingleClickOnConnectionOpensIt);

            if (pageRegSettingsInstance.SingleClickSwitchesToOpenConnection.IsSet)
                DisableControl(chkSingleClickOnOpenedConnectionSwitchesToIt);

            if (pageRegSettingsInstance.TrackActiveConnectionInConnectionTree.IsSet)
                DisableControl(chkConnectionTreeTrackActiveConnection);

            if (pageRegSettingsInstance.SetHostnameLikeDisplayName.IsSet)
                DisableControl(chkHostnameLikeDisplayName);

            if (pageRegSettingsInstance.DoNotTrimUsername.IsSet)
                DisableControl(chkDoNotTrimUsername);

            if (pageRegSettingsInstance.SlowClickRenameEnabled.IsSet)
                DisableControl(chkSlowClickRename);

            if (pageRegSettingsInstance.OpenMultipleConnectionsWithEnter.IsSet)
                DisableControl(chkOpenMultipleConnectionsWithEnter);

            if (pageRegSettingsInstance.RdpReconnectionCount.IsSet)
                DisableControl(numRdpReconnectionCount);

            if (pageRegSettingsInstance.ConRDPOverallConnectionTimeout.IsSet)
                DisableControl(numRDPConTimeout);

            if (pageRegSettingsInstance.AutoSaveEveryMinutes.IsSet)
                DisableControl(numAutoSave);

            // Updates the visibility of the information label indicating whether registry settings are used.
            lblRegistrySettingsUsedInfo.Visible = ShowRegistrySettingsUsedInfo();
        }

        /// <summary>
        /// Checks if specific registry settings related to appearence page are used.
        /// </summary>
        public bool ShowRegistrySettingsUsedInfo()
        {
            return pageRegSettingsInstance.SingleClickOnConnectionOpensIt.IsSet
                || pageRegSettingsInstance.SingleClickSwitchesToOpenConnection.IsSet
                || pageRegSettingsInstance.TrackActiveConnectionInConnectionTree.IsSet
                || pageRegSettingsInstance.SetHostnameLikeDisplayName.IsSet
                || pageRegSettingsInstance.DoNotTrimUsername.IsSet
                || pageRegSettingsInstance.SlowClickRenameEnabled.IsSet
                || pageRegSettingsInstance.OpenMultipleConnectionsWithEnter.IsSet
                || pageRegSettingsInstance.RdpReconnectionCount.IsSet
                || pageRegSettingsInstance.ConRDPOverallConnectionTimeout.IsSet
                || pageRegSettingsInstance.AutoSaveEveryMinutes.IsSet;
        }
    }
}
