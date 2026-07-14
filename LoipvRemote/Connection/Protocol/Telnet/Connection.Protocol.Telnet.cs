using System.Runtime.Versioning;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.Protocols.Putty;

namespace LoipvRemote.Connection.Protocol.Telnet
{
    [SupportedOSPlatform("windows")]
    public class ProtocolTelnet : PuttyBase
    {
        public ProtocolTelnet(ExternalCredentialConnectorRegistry externalCredentialConnectors, IStringSecretStore userSecretStore) : base(externalCredentialConnectors, userSecretStore)
        {
            PuttyProtocol = PuttyProtocolKind.Telnet;
        }

        public enum Defaults
        {
            Port = 23
        }
    }
}
