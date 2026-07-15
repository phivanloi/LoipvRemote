using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using LoipvRemote.App.Info;
using LoipvRemote.App.Initialization;
using LoipvRemote.Config.Connections.Multiuser;
using LoipvRemote.Config.Settings.Registry;
using LoipvRemote.Connection;
using LoipvRemote.Messages;
using LoipvRemote.Properties;
using LoipvRemote.Tools;
using LoipvRemote.Tools.Cmdline;
using LoipvRemote.UI;


namespace LoipvRemote.App
{
    [SupportedOSPlatform("windows")]
    public class Startup
    {
        private readonly ConnectionIconLoader _connectionIconLoader;

        public Startup()
        {
            _connectionIconLoader = new ConnectionIconLoader(GeneralAppInfo.HomePath + "\\Icons\\");
        }

        public void InitializeProgram(MessageCollector messageCollector)
        {
            RegistryLoader.Initialize(messageCollector);
            Debug.Print("---------------------------" + Environment.NewLine + "[START] - " + Convert.ToString(DateTime.Now, CultureInfo.InvariantCulture));
            StartupDataLogger startupLogger = new(messageCollector);
            startupLogger.LogStartupData();
            CompatibilityChecker.CheckCompatibility(messageCollector);
            ParseCommandLineArgs(messageCollector);
            _connectionIconLoader.GetConnectionIcons();
            DefaultConnectionInfo.Instance.LoadFrom(Settings.Default, a => "ConDefault" + a);
            DefaultConnectionInheritance.Instance.LoadFrom(Settings.Default, a => "InhDefault" + a);
        }

        private static void ParseCommandLineArgs(MessageCollector messageCollector)
        {
            StartupArgumentsInterpreter interpreter = new(messageCollector);
            interpreter.ParseArguments(Environment.GetCommandLineArgs());
        }

        public void CreateConnectionsProvider(MessageCollector messageCollector, IConnectionTreeWorkspace workspace)
        {
            ArgumentNullException.ThrowIfNull(messageCollector);
            ArgumentNullException.ThrowIfNull(workspace);
            messageCollector.AddMessage(MessageClass.DebugMsg, "Determining if we need a database syncronizer");
            if (!Properties.OptionsDBsPage.Default.UseSQLServer) return;
            messageCollector.AddMessage(MessageClass.DebugMsg, "Creating database syncronizer");
            workspace.RemoteConnectionsSyncronizer = new RemoteConnectionsSyncronizer(
                new ConnectionStoreUpdateChecker(workspace, messageCollector),
                workspace);
            workspace.RemoteConnectionsSyncronizer.Enable();
        }

    }
}
