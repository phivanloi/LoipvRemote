using System.Runtime.Versioning;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;

namespace LoipvRemote.Connection.Protocol.RDP
{
    [SupportedOSPlatform("windows")]
    public class RdpProtocol11 : RdpProtocol10
    {
        public RdpProtocol11(ExternalCredentialConnectorRegistry externalCredentialConnectors, IStringSecretStore userSecretStore) : base(externalCredentialConnectors, userSecretStore)
        {
        }

        protected override RdpVersion RdpProtocolVersion => global::LoipvRemote.Domain.Protocols.Rdp.RdpVersion.Rdc11;

        public override bool Initialize()
        {
            if (!base.Initialize())
                return false;

            if (RdpVersion < Versions.RDC100) return false; // minimum dll version checked, loaded MSTSCLIB dll version is not capable

            return true;
        }

    }
}
