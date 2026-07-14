using System.Runtime.Versioning;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.Protocols.Putty;

namespace LoipvRemote.Connection.Protocol.Rlogin
{
    [SupportedOSPlatform("windows")]
    public class ProtocolRlogin : PuttyBase
    {
        public ProtocolRlogin(ExternalCredentialConnectorRegistry externalCredentialConnectors, IStringSecretStore userSecretStore) : base(externalCredentialConnectors, userSecretStore)
        {
            PuttyProtocol = PuttyProtocolKind.Rlogin;
        }

        public enum Defaults
        {
            Port = 513
        }
    }
}
