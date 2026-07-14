using System.Runtime.Versioning;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.Protocols.Putty;

namespace LoipvRemote.Connection.Protocol.SSH
{
    [SupportedOSPlatform("windows")]
    public class ProtocolSSH2 : PuttyBase
    {
        public ProtocolSSH2(ExternalCredentialConnectorRegistry externalCredentialConnectors, IStringSecretStore userSecretStore) : base(externalCredentialConnectors, userSecretStore)
        {
            PuttyProtocol = PuttyProtocolKind.Ssh;
            PuttySSHVersion = PuttySshVersion.Ssh2;
        }

        public enum Defaults
        {
            Port = 22
        }
    }
}
