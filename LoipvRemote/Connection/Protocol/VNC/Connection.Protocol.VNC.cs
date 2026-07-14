using System;
using System.ComponentModel;
using LoipvRemote.Tools;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;
using LoipvRemote.Security;
using LoipvRemote.Protocols.Vnc;

// ReSharper disable ArrangeAccessorOwnerBody


namespace LoipvRemote.Connection.Protocol.VNC
{
    [SupportedOSPlatform("windows")]
    public class ProtocolVNC : ProtocolBase
    {
        #region Private Declarations

        private readonly VncDesktopClient _vnc = new();
        private ConnectionInfo _info;
        private readonly VncEndpointProbe _endpointProbe = new();
        private VncSession? _session;

        #endregion

        #region Public Methods

        public ProtocolVNC()
        {
            Control = _vnc.Control;
        }

        public override bool Initialize()
        {
            base.Initialize();

            try
            {
                _info = InterfaceControl.Info;
                if (_info is null)
                    return false;

                _session = new VncSession(_vnc.Session, _endpointProbe);
                return _session.Initialize(new VncConnectionOptions(
                    _info.Hostname,
                    _info.Port,
                    _info.VNCViewOnly,
                    _info.VNCSmartSizeMode != SmartSizeMode.SmartSNo));
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(Messages.MessageClass.ErrorMsg,
                                                    Language.VncSetPropsFailed + Environment.NewLine + ex.Message,
                                                    true);
                return false;
            }
        }

        public override bool Connect()
        {
            SetEventHandlers();
            try
            {
                if (_session is null || !_session.Connect())
                    return false;
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(Messages.MessageClass.ErrorMsg,
                                                    Language.ConnectionOpenFailed + Environment.NewLine +
                                                    ex.Message);
                return false;
            }

            return true;
        }

        public override void Disconnect()
        {
            try
            {
                _session?.Disconnect();
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(Messages.MessageClass.ErrorMsg,
                                                    Language.VncConnectionDisconnectFailed + Environment.NewLine +
                                                    ex.Message, true);
            }
        }

        public void SendSpecialKeys(SpecialKeys Keys)
        {
            try
            {
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (Keys)
                {
                    case SpecialKeys.CtrlAltDel:
                        _vnc.SendSpecialKeys(VncSpecialKeys.CtrlAltDel);
                        break;
                    case SpecialKeys.CtrlEsc:
                        _vnc.SendSpecialKeys(VncSpecialKeys.CtrlEsc);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(Messages.MessageClass.ErrorMsg,
                                                    Language.VncSendSpecialKeysFailed + Environment.NewLine +
                                                    ex.Message, true);
            }
        }

        public void StartChat()
        {
            throw new NotImplementedException();
        }

        public void StartFileTransfer()
        {
            throw new NotImplementedException();
        }

        public void RefreshScreen()
        {
            try
            {
                _vnc.RefreshScreen();
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(Messages.MessageClass.ErrorMsg,
                                                    Language.VncRefreshFailed + Environment.NewLine + ex.Message,
                                                    true);
            }
        }

        #endregion

        #region Private Methods

        private void SetEventHandlers()
        {
            try
            {
                _vnc.Connected += VNCEvent_Connected;
                _vnc.Disconnected += VNCEvent_Disconnected;
                ClipboardChangedSource.ClipboardChanged += VNCEvent_ClipboardChanged;
                if (!Force.HasFlag(ConnectionInfo.Force.NoCredentials) && _info?.Password?.Length > 0)
                {
                    _vnc.PasswordProvider = VNCEvent_Authenticate;
                }
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(Messages.MessageClass.ErrorMsg,
                                                    Language.VncSetEventHandlersFailed + Environment.NewLine +
                                                    ex.Message, true);
            }
        }

        #endregion

        #region Private Events & Handlers

        private void VNCEvent_Connected(object sender, EventArgs e)
        {
            Event_Connected(this);
            _vnc.AutoScroll = _info.VNCSmartSizeMode == SmartSizeMode.SmartSNo;
        }

        private void VNCEvent_Disconnected(object sender, EventArgs e)
        {
            ClipboardChangedSource.ClipboardChanged -= VNCEvent_ClipboardChanged;
            Event_Disconnected(this, @"VncSharp Disconnected.", null);
            Close();
        }

        private void VNCEvent_ClipboardChanged()
        {
            _vnc.FillServerClipboard();
        }

        private string VNCEvent_Authenticate()
        {
            //return _info.Password.ConvertToUnsecureString();
            return _info.Password;
        }

        #endregion

        #region Enums

        public enum Defaults
        {
            Port = 5900
        }

        public enum SpecialKeys
        {
            CtrlAltDel,
            CtrlEsc
        }

        public enum Compression
        {
            [LocalizedAttributes.LocalizedDescription(nameof(Language.NoCompression))]
            CompNone = 99,
            [Description("0")] Comp0 = 0,
            [Description("1")] Comp1 = 1,
            [Description("2")] Comp2 = 2,
            [Description("3")] Comp3 = 3,
            [Description("4")] Comp4 = 4,
            [Description("5")] Comp5 = 5,
            [Description("6")] Comp6 = 6,
            [Description("7")] Comp7 = 7,
            [Description("8")] Comp8 = 8,
            [Description("9")] Comp9 = 9
        }

        public enum Encoding
        {
            [Description("Raw")] EncRaw,
            [Description("RRE")] EncRRE,
            [Description("CoRRE")] EncCorre,
            [Description("Hextile")] EncHextile,
            [Description("Zlib")] EncZlib,
            [Description("Tight")] EncTight,
            [Description("ZlibHex")] EncZLibHex,
            [Description("ZRLE")] EncZRLE
        }

        public enum AuthMode
        {
            [LocalizedAttributes.LocalizedDescription(nameof(Language.Vnc))]
            AuthVNC,

            [LocalizedAttributes.LocalizedDescription(nameof(Language.Windows))]
            AuthWin
        }

        public enum ProxyType
        {
            [LocalizedAttributes.LocalizedDescription(nameof(Language.None))]
            ProxyNone,

            [LocalizedAttributes.LocalizedDescription(nameof(Language.Http))]
            ProxyHTTP,

            [LocalizedAttributes.LocalizedDescription(nameof(Language.Socks5))]
            ProxySocks5,

            [LocalizedAttributes.LocalizedDescription(nameof(Language.UltraVncRepeater))]
            ProxyUltra
        }

        public enum Colors
        {
            [LocalizedAttributes.LocalizedDescription(nameof(Language.Normal))]
            ColNormal,
            [Description("8-bit")] Col8Bit
        }

        public enum SmartSizeMode
        {
            [LocalizedAttributes.LocalizedDescription(nameof(Language.NoSmartSize))]
            SmartSNo,

            [LocalizedAttributes.LocalizedDescription(nameof(Language.Free))]
            SmartSFree,

            [LocalizedAttributes.LocalizedDescription(nameof(Language.Aspect))]
            SmartSAspect
        }

        #endregion
    }
}
