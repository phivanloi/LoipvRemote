using System.Runtime.Versioning;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.Protocols.Putty;

namespace LoipvRemote.Connection.Protocol.SSH
{
    [SupportedOSPlatform("windows")]
    public class ProtocolSSH1 : PuttyBase
    {
        public ProtocolSSH1(ExternalCredentialConnectorRegistry externalCredentialConnectors, IStringSecretStore userSecretStore) : base(externalCredentialConnectors, userSecretStore)
        {
            PuttyProtocol = PuttyProtocolKind.Ssh;
            PuttySSHVersion = PuttySshVersion.Ssh1;
        }

        public enum Defaults
        {
            Port = 22
        }
    }
}
