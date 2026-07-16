using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.App.Info;
using LoipvRemote.Messages;
using LoipvRemote.Properties;
using LoipvRemote.Resources.Language;


namespace LoipvRemote.Tools.Cmdline
{
    [SupportedOSPlatform("windows")]
    public class StartupArgumentsInterpreter
    {
        private readonly MessageCollector _messageCollector;

        public StartupArgumentsInterpreter(MessageCollector messageCollector)
        {
            ArgumentNullException.ThrowIfNull(messageCollector);

            _messageCollector = messageCollector;
        }

        public void ParseArguments(IEnumerable<string> cmdlineArgs)
        {
            //if (!cmdlineArgs.Any()) return;
            _messageCollector.AddMessage(MessageClass.DebugMsg, "Parsing cmdline arguments");

            try
            {
                CmdArgumentsInterpreter args = new(cmdlineArgs, _messageCollector);

                ParseResetPositionArg(args);
                ParseResetPanelsArg(args);
                ParseResetToolbarArg(args);
                ParseNoReconnectArg(args);
                ParseCustomConnectionPathArg(args);
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage(Language.CommandLineArgsCouldNotBeParsed, ex, logOnly: false);
            }
        }

        private void ParseResetPositionArg(CmdArgumentsInterpreter args)
        {
            if (args["resetpos"] == null && args["rp"] == null && args["reset"] == null) return;
            _messageCollector.AddMessage(MessageClass.DebugMsg, "Cmdline arg: Resetting window positions.");
            Properties.App.Default.MainFormKiosk = false;
            int newWidth = 900;
            int newHeight = 600;
            Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
            int newX = workingArea.Width / 2 - newWidth / 2;
            int newY = workingArea.Height / 2 - newHeight / 2;
            Properties.App.Default.MainFormLocation = new Point(newX, newY);
            Properties.App.Default.MainFormSize = new Size(newWidth, newHeight);
            Properties.App.Default.MainFormState = FormWindowState.Normal;
        }

        private void ParseResetPanelsArg(CmdArgumentsInterpreter args)
        {
            if (args["resetpanels"] == null && args["rpnl"] == null && args["reset"] == null) return;
            _messageCollector.AddMessage(MessageClass.DebugMsg, "Cmdline arg: Resetting panels");
            Properties.App.Default.ResetPanels = true;
        }

        private void ParseResetToolbarArg(CmdArgumentsInterpreter args)
        {
            if (args["resettoolbar"] == null && args["rtbr"] == null && args["reset"] == null) return;
            _messageCollector.AddMessage(MessageClass.DebugMsg, "Cmdline arg: Resetting toolbar position");
            Properties.App.Default.ResetToolbars = true;
        }

        private void ParseNoReconnectArg(CmdArgumentsInterpreter args)
        {
            if (args["noreconnect"] == null && args["norc"] == null) return;
            _messageCollector.AddMessage(MessageClass.DebugMsg,
                                         "Cmdline arg: Disabling reconnection to previously connected hosts");
            Properties.OptionsAdvancedPage.Default.NoReconnect = true;
        }

        private void ParseCustomConnectionPathArg(CmdArgumentsInterpreter args)
        {
            string? consParam = null;
            if (args["cons"] is not null)
                consParam = "cons";
            if (args["c"] is not null)
                consParam = "c";

            if (string.IsNullOrEmpty(consParam)) return;
            string? customPath = args[consParam];
            if (string.IsNullOrWhiteSpace(customPath)) return;
            _messageCollector.AddMessage(MessageClass.DebugMsg, "Cmdline arg: loading connections from a custom path");
            if (File.Exists(customPath) == false)
            {
                string homePath = GeneralAppInfo.HomePath ?? AppContext.BaseDirectory;
                if (File.Exists(Path.Combine(homePath, customPath)))
                {
                    Properties.OptionsBackupPage.Default.LoadConsFromCustomLocation = true;
                    Properties.OptionsBackupPage.Default.BackupLocation = Path.Combine(homePath, customPath);
                    return;
                }

                if (!File.Exists(Path.Combine(ConnectionsFileInfo.DefaultConnectionsPath, customPath))) return;
                Properties.OptionsBackupPage.Default.LoadConsFromCustomLocation = true;
                Properties.OptionsBackupPage.Default.BackupLocation = Path.Combine(ConnectionsFileInfo.DefaultConnectionsPath, customPath);
            }
            else
            {
                Properties.OptionsBackupPage.Default.LoadConsFromCustomLocation = true;
                Properties.OptionsBackupPage.Default.BackupLocation = customPath;
            }
        }
    }
}
