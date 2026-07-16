using System.IO;
using System.Runtime.Versioning;
using LoipvRemote.Config.Connections;
using LoipvRemote.Connection;
using LoipvRemote.Properties;

namespace LoipvRemote.App.Initialization
{
    [SupportedOSPlatform("windows")]
    public sealed class CredsAndConsSetup(
        IConnectionTreeWorkspace workspace,
        ConnectionLoadingService connectionLoadingService,
        Func<bool>? isClosing = null)
    {
        private readonly SaveConnectionsOnEdit _saveConnectionsOnEdit = new(workspace, isClosing);

        public void LoadCredsAndCons()
        {
            if (Properties.App.Default.FirstStart && !Properties.OptionsBackupPage.Default.LoadConsFromCustomLocation && !File.Exists(workspace.GetStartupConnectionFileName()))
                _ = workspace.NewConnectionsFileAsync(workspace.GetStartupConnectionFileName());

            _ = connectionLoadingService.LoadConnectionsAsync();
        }
    }
}
