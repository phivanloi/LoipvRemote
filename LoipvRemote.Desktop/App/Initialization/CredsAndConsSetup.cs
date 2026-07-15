using System.IO;
using System.Runtime.Versioning;
using LoipvRemote.Config.Connections;
using LoipvRemote.Connection;
using LoipvRemote.Properties;

namespace LoipvRemote.App.Initialization
{
    [SupportedOSPlatform("windows")]
    public sealed class CredsAndConsSetup(IConnectionTreeWorkspace workspace, ConnectionLoadingService connectionLoadingService)
    {
        public void LoadCredsAndCons()
        {
            new SaveConnectionsOnEdit(workspace);

            if (Properties.App.Default.FirstStart && !Properties.OptionsBackupPage.Default.LoadConsFromCustomLocation && !File.Exists(workspace.GetStartupConnectionFileName()))
                workspace.NewConnectionsFile(workspace.GetStartupConnectionFileName());

            connectionLoadingService.LoadConnections();
        }
    }
}
