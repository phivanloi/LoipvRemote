using System;
using System.Drawing;
using System.Windows.Forms;
using LoipvRemote.Infrastructure.Windows.ProcessManagement;
using LoipvRemote.Messages;
using LoipvRemote.Properties;
using LoipvRemote.Tools;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.ExternalApps;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;

namespace LoipvRemote.Connection.Protocol
{
    [SupportedOSPlatform("windows")]
    public class IntegratedProgram : ProtocolBase
    {
        #region Private Fields

        private ExternalTool _externalTool;
        private ExternalApplicationSession? _session;
        private readonly IExternalApplicationHostFactory _externalApplicationHostFactory;

        #endregion

        public IntegratedProgram(IExternalApplicationHostFactory externalApplicationHostFactory)
        {
            _externalApplicationHostFactory = externalApplicationHostFactory
                ?? throw new ArgumentNullException(nameof(externalApplicationHostFactory));
        }

        #region Public Methods

        public override bool Initialize()
        {
            if (InterfaceControl.Info == null)
                return base.Initialize();

            _externalTool = ExternalToolsService.GetExtAppByName(InterfaceControl.Info.ExtApp);

            if (_externalTool == null)
            {
                MessageCollector?.AddMessage(MessageClass.ErrorMsg,
                                                     string.Format(Language.CouldNotFindExternalTool,
                                                                   InterfaceControl.Info.ExtApp));
                return false;
            }

            _externalTool.ConnectionInfo = InterfaceControl.Info;

            return base.Initialize();
        }

        public override bool Connect()
        {
            try
            {
                MessageCollector?.AddMessage(MessageClass.InformationMsg,
                                                     $"Attempting to start: {_externalTool.DisplayName}", true);

                if (_externalTool.TryIntegrate == false)
                {
                    _externalTool.Start(InterfaceControl.Info);
                    /* Don't call close here... There's nothing for the override to do in this case since
                     * _process is not created in this scenario. When returning false, ProtocolBase.Close()
                     * will be called - which is just going to call IntegratedProgram.Close() again anyway...
                     * Close();
                     */
                    MessageCollector?.AddMessage(MessageClass.InformationMsg,
                                                         $"Assuming no other errors/exceptions occurred immediately before this message regarding {_externalTool.DisplayName}, the next \"closed by user\" message can be ignored",
                                                         true);
                    return false;
                }

                _session = new ExternalApplicationSession(
                    _externalTool.ToDefinition(InterfaceControl.Info),
                    _externalApplicationHostFactory.Create());
                _session.Exited += SessionOnExited;

                if (!_session.Initialize() || !_session.Connect())
                    return false;

                TimeSpan windowTimeout = TimeSpan.FromSeconds(Properties.OptionsAdvancedPage.Default.MaxPuttyWaitTime);
                if (!_session.AttachTo(InterfaceControl.Handle, windowTimeout))
                {
                    _session.Dispose();
                    _session = null;
                    return false;
                }

                MessageCollector?.AddMessage(MessageClass.InformationMsg, Language.IntAppStuff, true);
                MessageCollector?.AddMessage(MessageClass.InformationMsg,
                                                     string.Format(Language.IntAppHandle, _session.WindowHandle), true);
                MessageCollector?.AddMessage(MessageClass.InformationMsg,
                                                     string.Format(Language.IntAppTitle, _session.WindowTitle),
                                                     true);
                MessageCollector?.AddMessage(MessageClass.InformationMsg,
                                                     string.Format(Language.PanelHandle,
                                                                   InterfaceControl.Parent.Handle), true);

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
                _session?.Focus();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage(Language.IntAppFocusFailed, ex);
            }
        }

        protected override void Resize(object sender, EventArgs e)
        {
            try
            {
                if (InterfaceControl.Size == Size.Empty) return;
                // The shell owns the WinForms client bounds; the infrastructure adapter owns MoveWindow.
                Rectangle clientRect = InterfaceControl.ClientRectangle;
                _session?.Resize(new EmbeddedWindowBounds(
                    clientRect.X - SystemInformation.FrameBorderSize.Width,
                    clientRect.Y - (SystemInformation.CaptionHeight + SystemInformation.FrameBorderSize.Height),
                    clientRect.Width + SystemInformation.FrameBorderSize.Width * 2,
                    clientRect.Height + SystemInformation.CaptionHeight +
                    SystemInformation.FrameBorderSize.Height * 2));
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage(Language.IntAppResizeFailed, ex);
            }
        }

        public override void Close()
        {
            if (_session is not null)
            {
                try
                {
                    _session.Exited -= SessionOnExited;
                    _session.Dispose();
                }
                catch (Exception ex)
                {
                    MessageCollector.AddExceptionMessage(Language.IntAppKillFailed, ex);
                }
                finally
                {
                    _session = null;
                }
            }

            base.Close();
        }

        #endregion

        #region Private Methods

        private void SessionOnExited(object? sender, EventArgs e)
        {
            Event_Closed(this);
        }

        #endregion

        #region Enumerations

        public enum Defaults
        {
            Port = 0
        }

        #endregion
    }
}
