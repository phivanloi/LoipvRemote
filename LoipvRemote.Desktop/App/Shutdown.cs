using LoipvRemote.Tools;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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

        public static async Task CleanupAsync(
            Control quickConnectToolStrip,
            ExternalToolsToolStrip externalToolsToolStrip,
            MultiSshToolStrip multiSshToolStrip,
            FrmMain frmMain,
            DesktopShellRuntime desktopShellRuntime,
            CancellationToken cancellationToken = default)
        {
            try
            {
                StopPuttySessionWatcher(desktopShellRuntime.PuttySessionsManager);
                DisposeNotificationAreaIcon(desktopShellRuntime.RuntimeState);
                await SaveConnectionsAsync(desktopShellRuntime.ConnectionWorkspaceRuntime, cancellationToken).ConfigureAwait(true);
                SaveSettings(quickConnectToolStrip, externalToolsToolStrip, multiSshToolStrip, frmMain, desktopShellRuntime);
            }
            catch (Exception ex)
            {
                desktopShellRuntime.MessageCollector.AddExceptionStackTrace(Language.SettingsCouldNotBeSavedOrTrayDispose, ex);
            }
        }

        private static void StopPuttySessionWatcher(PuttySessionsManager puttySessionsManager)
        {
            puttySessionsManager.StopWatcher();
        }

        private static void DisposeNotificationAreaIcon(RuntimeState runtimeState)
        {
            if (runtimeState.NotificationAreaIcon is { Disposed: false } notificationAreaIcon)
                notificationAreaIcon.Dispose();
        }

        private static async Task SaveConnectionsAsync(
            IConnectionTreeWorkspace workspace,
            CancellationToken cancellationToken)
        {
            DateTime lastUpdate;
            DateTime updateDate;
            DateTime currentDate = DateTime.Now;

            if ((Properties.OptionsBackupPage.Default.SaveConnectionsFrequency == (int)ConnectionsBackupFrequency.OnExit))
            {

                await workspace.SaveConnectionsAsync(cancellationToken).ConfigureAwait(true);
                return;
            }
            lastUpdate = workspace.UsingDatabase ? workspace.LastSqlUpdate : workspace.LastFileUpdate;

            switch (Properties.OptionsBackupPage.Default.SaveConnectionsFrequency)
            {
                case (int)ConnectionsBackupFrequency.Daily:
                    updateDate = lastUpdate.AddDays(1);
                    break;
                case (int)ConnectionsBackupFrequency.Weekly:
                    updateDate = lastUpdate.AddDays(7);
                    break;
                default:
                    return;
            }

            if (currentDate >= updateDate)
            {
                await workspace.SaveConnectionsAsync(cancellationToken).ConfigureAwait(true);
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
