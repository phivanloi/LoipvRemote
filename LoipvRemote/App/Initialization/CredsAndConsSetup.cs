using System.IO;
using System.Runtime.Versioning;
using LoipvRemote.Config.Connections;
using LoipvRemote.Connection;
using LoipvRemote.Properties;

namespace LoipvRemote.App.Initialization
{
    [SupportedOSPlatform("windows")]
    public sealed class CredsAndConsSetup(ConnectionsService connectionsService, ConnectionLoadingService connectionLoadingService)
    {
        public void LoadCredsAndCons()
        {
            new SaveConnectionsOnEdit(connectionsService);

            if (Properties.App.Default.FirstStart && !Properties.OptionsBackupPage.Default.LoadConsFromCustomLocation && !File.Exists(connectionsService.GetStartupConnectionFileName()))
                connectionsService.NewConnectionsFile(connectionsService.GetStartupConnectionFileName());

            connectionLoadingService.LoadConnections();
        }
    }
}
