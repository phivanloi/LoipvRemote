using System.Runtime.Versioning;

namespace LoipvRemote.App.Info
{
    [SupportedOSPlatform("windows")]
    public class CredentialsFileInfo
    {
        public static readonly string CredentialsPath = SettingsFileInfo.SettingsPath;
        public static readonly string CredentialsFile = "confCreds.xml";
        public static readonly string NewCredentialsFileName = "confCredsNew.xml";
        public static readonly double CredentialsFileVersion = 1.0;
    }
}
