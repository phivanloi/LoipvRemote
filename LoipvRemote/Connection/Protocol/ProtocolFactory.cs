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
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.ExternalApps;
using LoipvRemote.UseCases.Sessions;
using LoipvRemote.Domain.Connections;
using System.Runtime.Versioning;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.Messages;
using LoipvRemote.UI.Adapters;
using LoipvRemote.Tools;

namespace LoipvRemote.Connection.Protocol
{
    [SupportedOSPlatform("windows")]
    public class ProtocolFactory : IProtocolSessionFactory<ConnectionInfo>, IProtocolFactory
    {
        private readonly RdpProtocolFactory _rdpProtocolFactory;
        private readonly ExternalCredentialConnectorRegistry _externalCredentialConnectors;
        private readonly IStringSecretStore _userSecretStore;
        private readonly MessageCollector _messageCollector;
        private readonly ConnectionWorkspaceAdapter _connectionWorkspace;
        private readonly ExternalToolsService _externalToolsService;
        private readonly IExternalApplicationHostFactory _externalApplicationHostFactory;

        public ProtocolFactory(
            ExternalCredentialConnectorRegistry externalCredentialConnectors,
            IStringSecretStore userSecretStore,
            MessageCollector messageCollector,
            ConnectionWorkspaceAdapter connectionWorkspace,
            ExternalToolsService externalToolsService,
            IExternalApplicationHostFactory externalApplicationHostFactory)
        {
            _externalCredentialConnectors = externalCredentialConnectors
                ?? throw new ArgumentNullException(nameof(externalCredentialConnectors));
            _userSecretStore = userSecretStore ?? throw new ArgumentNullException(nameof(userSecretStore));
            _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
            _connectionWorkspace = connectionWorkspace ?? throw new ArgumentNullException(nameof(connectionWorkspace));
            _externalToolsService = externalToolsService ?? throw new ArgumentNullException(nameof(externalToolsService));
            _externalApplicationHostFactory = externalApplicationHostFactory ?? throw new ArgumentNullException(nameof(externalApplicationHostFactory));
            _rdpProtocolFactory = new RdpProtocolFactory(_externalCredentialConnectors, _userSecretStore, _messageCollector);
        }

        public ProtocolBase CreateProtocol(ConnectionInfo connectionInfo)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (connectionInfo.Protocol)
            {
                case ProtocolType.RDP:
                    RdpProtocol rdp = _rdpProtocolFactory.Build(connectionInfo.RdpVersion);
                    rdp.LoadBalanceInfoUseUtf8 = Properties.OptionsAdvancedPage.Default.RdpLoadBalanceInfoUseUtf8;
                    return Attach(rdp);
                case ProtocolType.VNC:
                    return Attach(new ProtocolVNC());
                case ProtocolType.ARD:
                    return Attach(new ProtocolARD());
                case ProtocolType.SSH1:
                    return Attach(new ProtocolSSH1(_externalCredentialConnectors, _userSecretStore));
                case ProtocolType.SSH2:
                    return Attach(new ProtocolSSH2(_externalCredentialConnectors, _userSecretStore));
                case ProtocolType.Telnet:
                    return Attach(new ProtocolTelnet(_externalCredentialConnectors, _userSecretStore));
                case ProtocolType.Rlogin:
                    return Attach(new ProtocolRlogin(_externalCredentialConnectors, _userSecretStore));
                case ProtocolType.RAW:
                    return Attach(new RawProtocol(_externalCredentialConnectors, _userSecretStore));
                case ProtocolType.HTTP:
                    return Attach(new ProtocolHTTP(connectionInfo.RenderingEngine));
                case ProtocolType.HTTPS:
                    return Attach(new ProtocolHTTPS(connectionInfo.RenderingEngine));
                case ProtocolType.PowerShell:
                    return Attach(new ProtocolPowerShell(connectionInfo));
                case ProtocolType.WSL:
                    return Attach(new ProtocolWSL(connectionInfo));
                case ProtocolType.Terminal:
                    return Attach(new ProtocolTerminal(connectionInfo));
                case ProtocolType.AnyDesk:
                    return Attach(new ProtocolAnyDesk(connectionInfo));
                case ProtocolType.IntApp:
                    if (connectionInfo.ExtApp == "")
                    {
                        throw (new Exception(Language.NoExtAppDefined));
                    }
                    return Attach(new IntegratedProgram(_externalApplicationHostFactory));
            }

            return default(ProtocolBase);
        }

        IProtocolSession IProtocolSessionFactory<ConnectionInfo>.Create(ConnectionInfo request)
        {
            return CreateProtocol(request);
        }

        IProtocolSession IProtocolFactory.Create(ConnectionDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (definition.Protocol == ProtocolKind.ExternalApplication)
            {
                if (definition.ExternalApplication is null)
                    throw new ArgumentException("External application sessions require a command definition.", nameof(definition));

                return new ExternalApplicationSession(
                    definition.ExternalApplication,
                    _externalApplicationHostFactory.Create());
            }

            var connection = new ConnectionInfo(definition.Id.ToString())
            {
                Name = definition.Name,
                Hostname = definition.Host,
                Port = definition.Port,
                Protocol = MapProtocol(definition.Protocol)
            };

            return CreateProtocol(connection)
                   ?? throw new NotSupportedException($"Protocol '{definition.Protocol}' is not available in the current desktop host.");
        }

        private static ProtocolType MapProtocol(ProtocolKind protocol) => protocol switch
        {
            ProtocolKind.Rdp => ProtocolType.RDP,
            ProtocolKind.Vnc => ProtocolType.VNC,
            ProtocolKind.Ssh1 => ProtocolType.SSH1,
            ProtocolKind.Ssh2 => ProtocolType.SSH2,
            ProtocolKind.Telnet => ProtocolType.Telnet,
            ProtocolKind.Rlogin => ProtocolType.Rlogin,
            ProtocolKind.Raw => ProtocolType.RAW,
            ProtocolKind.Http or ProtocolKind.Browser => ProtocolType.HTTP,
            ProtocolKind.Https => ProtocolType.HTTPS,
            ProtocolKind.Ard => ProtocolType.ARD,
            ProtocolKind.PowerShell => ProtocolType.PowerShell,
            ProtocolKind.Terminal => ProtocolType.Terminal,
            ProtocolKind.Wsl => ProtocolType.WSL,
            ProtocolKind.AnyDesk => ProtocolType.AnyDesk,
            _ => throw new NotSupportedException($"Protocol '{protocol}' is not available in the current desktop host.")
        };

        private ProtocolBase Attach(ProtocolBase protocol)
        {
            _connectionWorkspace.TryGetMainWindow(out var mainWindow);
            protocol.AttachServices(_messageCollector, mainWindow, _externalToolsService, _connectionWorkspace);
            return protocol;
        }
    }
}
