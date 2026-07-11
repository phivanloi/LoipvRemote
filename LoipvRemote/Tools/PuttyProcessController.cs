using LoipvRemote.Properties;
using LoipvRemote.Tools.Cmdline;
using System.Runtime.Versioning;

namespace LoipvRemote.Tools
{
    [SupportedOSPlatform("windows")]
    public class PuttyProcessController : ProcessController
    {
        public bool Start(CommandLineArguments arguments = null)
        {
            string filename = Properties.OptionsAdvancedPage.Default.UseCustomPuttyPath ? Properties.OptionsAdvancedPage.Default.CustomPuttyPath : App.Info.GeneralAppInfo.PuttyPath;
            return Start(filename, arguments);
        }
    }
}