using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.Messages;
using LoipvRemote.Protocols.Rdp;

namespace LoipvRemote.Connection.Protocol.RDP
{
    [SupportedOSPlatform("windows")]
    public class RdpProtocolFactory
    {
        private readonly ExternalCredentialConnectorRegistry _externalCredentialConnectors;
        private readonly IStringSecretStore _userSecretStore;
        private readonly MessageCollector _messageCollector;

        public RdpProtocolFactory(
            ExternalCredentialConnectorRegistry externalCredentialConnectors,
            IStringSecretStore userSecretStore,
            MessageCollector messageCollector)
        {
            _externalCredentialConnectors = externalCredentialConnectors
                ?? throw new ArgumentNullException(nameof(externalCredentialConnectors));
            _userSecretStore = userSecretStore ?? throw new ArgumentNullException(nameof(userSecretStore));
            _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
        }

        public RdpProtocol Build(RdpVersion rdpVersion)
        {
            switch (rdpVersion)
            {
                case RdpVersion.Highest:
                    return BuildHighestSupportedVersion();
                case RdpVersion.Rdc6:
                    return Attach(new RdpProtocol(_externalCredentialConnectors, _userSecretStore));
                case RdpVersion.Rdc7:
                    return Attach(new RdpProtocol7(_externalCredentialConnectors, _userSecretStore));
                case RdpVersion.Rdc8:
                    return Attach(new RdpProtocol8(_externalCredentialConnectors, _userSecretStore));
                case RdpVersion.Rdc9:
                    return Attach(new RdpProtocol9(_externalCredentialConnectors, _userSecretStore));
                case RdpVersion.Rdc10:
                    return Attach(new RdpProtocol10(_externalCredentialConnectors, _userSecretStore));
                case RdpVersion.Rdc11:
                    return Attach(new RdpProtocol11(_externalCredentialConnectors, _userSecretStore));
                default:
                    throw new ArgumentOutOfRangeException(nameof(rdpVersion), rdpVersion, null);
            }
        }

        private RdpProtocol BuildHighestSupportedVersion()
        {
            RdpVersion selectedVersion = RdpVersionSelector.SelectHighestSupported(
                version => Build(version).RdpVersionSupported());
            return Build(selectedVersion);
        }

        public List<RdpVersion> GetSupportedVersions()
        {
            return RdpVersionSelector.GetSupportedVersions(
                    version => Build(version).RdpVersionSupported())
                .ToList();
        }

        private RdpProtocol Attach(RdpProtocol protocol)
        {
            protocol.AttachServices(_messageCollector);
            return protocol;
        }
    }
}
