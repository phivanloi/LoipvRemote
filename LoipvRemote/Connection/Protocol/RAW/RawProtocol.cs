using System.Runtime.Versioning;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.Protocols.Putty;

namespace LoipvRemote.Connection.Protocol.RAW
{
    [SupportedOSPlatform("windows")]
    public class RawProtocol : PuttyBase
    {
        public RawProtocol(ExternalCredentialConnectorRegistry externalCredentialConnectors, IStringSecretStore userSecretStore) : base(externalCredentialConnectors, userSecretStore)
        {
            PuttyProtocol = PuttyProtocolKind.Raw;
        }

        public enum Defaults
        {
            Port = 23
        }
    }
}
