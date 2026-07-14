using LoipvRemote.Messages;
using System;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;
using LoipvRemote.Infrastructure.Windows.Dpapi;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.Protocols.Rdp;

namespace LoipvRemote.Connection.Protocol.RDP
{
    [SupportedOSPlatform("windows")]
    public class RdpProtocol7 : RdpProtocol
    {
        public RdpProtocol7(ExternalCredentialConnectorRegistry externalCredentialConnectors, IStringSecretStore userSecretStore) : base(externalCredentialConnectors, userSecretStore)
        {
        }

        protected override RdpVersion RdpProtocolVersion => global::LoipvRemote.Domain.Protocols.Rdp.RdpVersion.Rdc7;

        public override bool Initialize()
        {
            if (!base.Initialize())
                return false;

            try
            {
                if (RdpVersion < Versions.RDC70) return false; // loaded MSTSCLIB dll version is not capable

                string pcb = connectionInfo.UseVmId
                    ? connectionInfo.VmId + (connectionInfo.UseEnhancedMode ? ";EnhancedMode=1" : string.Empty)
                    : string.Empty;
                string? encryptedAuthToken = null;
                if (connectionInfo.RDGatewayUseConnectionCredentials == RDGatewayUseConnectionCredentials.AccessToken)
                {
                    encryptedAuthToken = WindowsRdpGatewayTokenProtector.EncryptAuthCookieString(
                        connectionInfo.RDGatewayAccessToken);
                }
                Runtime.ConfigureVersion7(
                    (uint)connectionInfo.SoundQuality,
                    connectionInfo.RedirectAudioCapture,
                    (uint)RdpNetworkConnectionType.Modem,
                    connectionInfo.UseRedirectionServerName,
                    connectionInfo.UseVmId ? "Microsoft Virtual Console Service" : string.Empty,
                    pcb,
                    encryptedAuthToken);
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace(Language.RdpSetPropsFailed, ex);
                return false;
            }

            return true;
        }

    }
}
