using LoipvRemote.Connection.Protocol.Http;
using LoipvRemote.Connection.Protocol.RAW;
using LoipvRemote.Connection.Protocol.RDP;
using LoipvRemote.Connection.Protocol.Rlogin;
using LoipvRemote.Connection.Protocol.SSH;
using LoipvRemote.Connection.Protocol.Telnet;
using LoipvRemote.Connection.Protocol.VNC;
using LoipvRemote.Connection.Protocol.ARD;
using System;
using LoipvRemote.Connection.Protocol.PowerShell;
using LoipvRemote.Connection.Protocol.WSL;
using LoipvRemote.Connection.Protocol.Terminal;
using LoipvRemote.Connection.Protocol.AnyDesk;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;

namespace LoipvRemote.Connection.Protocol
{
    [SupportedOSPlatform("windows")]
    public class ProtocolFactory
    {
        private readonly RdpProtocolFactory _rdpProtocolFactory = new();

        public ProtocolBase CreateProtocol(ConnectionInfo connectionInfo)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (connectionInfo.Protocol)
            {
                case ProtocolType.RDP:
                    RdpProtocol rdp = _rdpProtocolFactory.Build(connectionInfo.RdpVersion);
                    rdp.LoadBalanceInfoUseUtf8 = Properties.OptionsAdvancedPage.Default.RdpLoadBalanceInfoUseUtf8;
                    return rdp;
                case ProtocolType.VNC:
                    return new ProtocolVNC();
                case ProtocolType.ARD:
                    return new ProtocolARD();
                case ProtocolType.SSH1:
                    return new ProtocolSSH1();
                case ProtocolType.SSH2:
                    return new ProtocolSSH2();
                case ProtocolType.Telnet:
                    return new ProtocolTelnet();
                case ProtocolType.Rlogin:
                    return new ProtocolRlogin();
                case ProtocolType.RAW:
                    return new RawProtocol();
                case ProtocolType.HTTP:
                    return new ProtocolHTTP(connectionInfo.RenderingEngine);
                case ProtocolType.HTTPS:
                    return new ProtocolHTTPS(connectionInfo.RenderingEngine);
                case ProtocolType.PowerShell:
                    return new ProtocolPowerShell(connectionInfo);
                case ProtocolType.WSL:
                    return new ProtocolWSL(connectionInfo);
                case ProtocolType.Terminal:
                    return new ProtocolTerminal(connectionInfo);
                case ProtocolType.AnyDesk:
                    return new ProtocolAnyDesk(connectionInfo);
                case ProtocolType.IntApp:
                    if (connectionInfo.ExtApp == "")
                    {
                        throw (new Exception(Language.NoExtAppDefined));
                    }
                    return new IntegratedProgram();
            }

            return default(ProtocolBase);
        }
    }
}