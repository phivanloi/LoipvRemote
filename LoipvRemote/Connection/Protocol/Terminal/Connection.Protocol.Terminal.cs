using System;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.Messages;
using LoipvRemote.Protocols.ExternalApps;
using LoipvRemote.Resources.Language;

namespace LoipvRemote.Connection.Protocol.Terminal
{
    [SupportedOSPlatform("windows")]
    public class ProtocolTerminal(ConnectionInfo connectionInfo) : ProtocolBase
    {
        #region Private Fields

        private IntPtr _handle;
        private readonly ConnectionInfo _connectionInfo = connectionInfo;
        private ExternalConsoleRuntime _consoleRuntime;

        #endregion

        #region Public Methods

        public override bool Connect()
        {
            try
            {
                MessageCollector?.AddMessage(MessageClass.InformationMsg, "Attempting to start Terminal session.", true);

                _consoleRuntime = new ExternalConsoleRuntime(ColorTranslator.FromHtml("#012456"));

                string commandProcessor = Environment.GetEnvironmentVariable("COMSPEC") ?? @"C:\Windows\System32\cmd.exe";
                TerminalProcessStartInfo process = TerminalProcessStartInfoBuilder.Build(
                    _connectionInfo.Hostname, _connectionInfo.Username, _connectionInfo.Port, commandProcessor);
                _consoleRuntime.StartProcess(process.FileName, process.Arguments);

                if (!_consoleRuntime.IsHandleCreated)
                {
                    throw new InvalidOperationException("Failed to initialize the managed terminal control.");
                }

                _handle = _consoleRuntime.Handle;
                EmbeddedWindowOperations.SetParent(_handle, InterfaceControl.Handle);

                Resize(this, new EventArgs());
                base.Connect();
                return true;
            }
            catch (Exception ex)
            {
                MessageCollector?.AddExceptionMessage(Language.ConnectionFailed, ex);
                return false;
            }
        }

        public override void Focus()
        {
            try
            {
                _consoleRuntime.Control.Focus();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage(Language.IntAppFocusFailed, ex);
            }
        }

        public override void Close()
        {
            _consoleRuntime?.Dispose();
            base.Close();
        }

        protected override void Resize(object sender, EventArgs e)
        {
            try
            {
                if (InterfaceControl.Size == Size.Empty) return;
                // Use ClientRectangle to account for padding (for connection frame color)
                Rectangle clientRect = InterfaceControl.ClientRectangle;
                EmbeddedWindowOperations.Move(_handle,
                                         clientRect.X - SystemInformation.FrameBorderSize.Width,
                                         clientRect.Y - (SystemInformation.CaptionHeight + SystemInformation.FrameBorderSize.Height),
                                         clientRect.Width + SystemInformation.FrameBorderSize.Width * 2,
                                         clientRect.Height + SystemInformation.CaptionHeight +
                                         SystemInformation.FrameBorderSize.Height * 2);
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage(Language.IntAppResizeFailed, ex);
            }
        }

        #endregion

        #region Enumerations

        public enum Defaults
        {
            Port = 22
        }

        #endregion
    }
}
