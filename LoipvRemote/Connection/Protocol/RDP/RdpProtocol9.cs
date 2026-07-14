using System;
using System.Runtime.Versioning;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;

namespace LoipvRemote.Connection.Protocol.RDP
{
    [SupportedOSPlatform("windows")]
    public class RdpProtocol9 : RdpProtocol8
    {
        public RdpProtocol9(ExternalCredentialConnectorRegistry externalCredentialConnectors, IStringSecretStore userSecretStore) : base(externalCredentialConnectors, userSecretStore)
        {
        }

        protected override RdpVersion RdpProtocolVersion => global::LoipvRemote.Domain.Protocols.Rdp.RdpVersion.Rdc9;

        // Constructor not needed - ResizeEnd is already registered in RdpProtocol8 base class

        public override bool Initialize()
        {
            if (!base.Initialize())
                return false;

            if (RdpVersion < Versions.RDC81) return false; // minimum dll version checked, loaded MSTSCLIB dll version is not capable

            return true;
        }

    }
}
