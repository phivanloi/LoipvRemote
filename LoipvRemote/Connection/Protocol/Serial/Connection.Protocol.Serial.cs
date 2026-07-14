using System.Runtime.Versioning;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.Protocols.Putty;

namespace LoipvRemote.Connection.Protocol.Serial
{
    [SupportedOSPlatform("windows")]
    public class ProtocolSerial : PuttyBase
    {
        public ProtocolSerial(ExternalCredentialConnectorRegistry externalCredentialConnectors, IStringSecretStore userSecretStore) : base(externalCredentialConnectors, userSecretStore)
        {
            PuttyProtocol = PuttyProtocolKind.Serial;
        }

        public enum Defaults
        {
            Port = 9600
        }
    }
}
