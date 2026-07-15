using LoipvRemote.Tools;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using LoipvRemote.Config.Connections;
using LoipvRemote.Config.Putty;
using LoipvRemote.Connection;
using LoipvRemote.App.Composition;
using LoipvRemote.Properties;
using LoipvRemote.UI.Controls;
using LoipvRemote.UI.Forms;
using LoipvRemote.Resources.Language;
using LoipvRemote.UseCases.Configuration;
using System.Runtime.Versioning;

// ReSharper disable ArrangeAccessorOwnerBody

namespace LoipvRemote.App
{
    [SupportedOSPlatform("windows")]
    public static class Shutdown
    {
        public static void Quit()
        {
            FrmMain.Default.Close();
            ProgramRoot.CloseSingletonInstanceMutex();
        }

        public static void Cleanup(
            Control quickConnectToolStrip,
            ExternalToolsToolStrip externalToolsToolStrip,
            MultiSshToolStrip multiSshToolStrip,
            FrmMain frmMain,
            DesktopShellRuntime desktopShellRuntime)
        {
            try
            {
                StopPuttySessionWatcher();
                DisposeNotificationAreaIcon(desktopShellRuntime.RuntimeState);
                SaveConnections(desktopShellRuntime.ConnectionWorkspaceRuntime);
                SaveSettings(quickConnectToolStrip, externalToolsToolStrip, multiSshToolStrip, frmMain, desktopShellRuntime);
            }
            catch (Exception ex)
            {
                desktopShellRuntime.MessageCollector.AddExceptionStackTrace(Language.SettingsCouldNotBeSavedOrTrayDispose, ex);
            }
        }

        private static void StopPuttySessionWatcher()
        {
            PuttySessionsManager.Instance.StopWatcher();
        }

        private static void DisposeNotificationAreaIcon(RuntimeState runtimeState)
        {
            if (runtimeState.NotificationAreaIcon is { Disposed: false } notificationAreaIcon)
                notificationAreaIcon.Dispose();
        }

        private static void SaveConnections(IConnectionWorkspace workspace)
        {
            DateTime lastUpdate;
            DateTime updateDate;
            DateTime currentDate = DateTime.Now;

            if ((Properties.OptionsBackupPage.Default.SaveConnectionsFrequency == (int)ConnectionsBackupFrequencyEnum.OnExit))
            {
                workspace.SaveConnections();
				return;
            }
			lastUpdate = workspace.UsingDatabase ? workspace.LastSqlUpdate : workspace.LastFileUpdate;

            switch (Properties.OptionsBackupPage.Default.SaveConnectionsFrequency)
            {
                case (int)ConnectionsBackupFrequencyEnum.Daily:
                    updateDate = lastUpdate.AddDays(1);
                    break;
                case (int)ConnectionsBackupFrequencyEnum.Weekly:
                    updateDate = lastUpdate.AddDays(7);
                    break;
                default:
                    return;
            }

            if (currentDate >= updateDate)
            {
                workspace.SaveConnections();
            }
        }

        private static void SaveSettings(
            Control quickConnectToolStrip,
            ExternalToolsToolStrip externalToolsToolStrip,
            MultiSshToolStrip multiSshToolStrip,
            FrmMain frmMain,
            DesktopShellRuntime desktopShellRuntime)
        {
            Config.Settings.SettingsSaver.SaveSettings(quickConnectToolStrip, externalToolsToolStrip, multiSshToolStrip,
                                                       frmMain, desktopShellRuntime.ExternalToolsService, desktopShellRuntime.MessageCollector);
        }

    }
}
