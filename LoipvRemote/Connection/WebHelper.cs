using LoipvRemote.App;
using LoipvRemote.Connection.Protocol;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;

namespace LoipvRemote.Connection
{
    [SupportedOSPlatform("windows")]
    public class WebHelper
    {
        public static void GoToUrl(string url)
        {
            ConnectionInfo connectionInfo = new();
            connectionInfo.CopyFrom(DefaultConnectionInfo.Instance);

            connectionInfo.Name = "";
            connectionInfo.Hostname = url;
            connectionInfo.Protocol = url.StartsWith("https:") ? ProtocolType.HTTPS : ProtocolType.HTTP;
            connectionInfo.SetDefaultPort();
            if (string.IsNullOrEmpty(connectionInfo.Panel))
                connectionInfo.Panel = Language.General;
            connectionInfo.IsQuickConnect = true;
            Runtime.ConnectionInitiator.OpenConnection(connectionInfo, ConnectionInfo.Force.DoNotJump);
        }
    }
}