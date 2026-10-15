using LoipvRemote.Domain.Metadata;

namespace LoipvRemote.Domain.Protocols.Rdp;

public enum RDGatewayUseConnectionCredentials
{
    [ProtocolDisplayKey("UseDifferentUsernameAndPassword")]
    No = 0,

    [ProtocolDisplayKey("UseSameUsernameAndPassword")]
    Yes = 1,

    [ProtocolDisplayKey("UseSmartCard")]
    SmartCard = 2,

    [ProtocolDisplayKey("UseAccessToken")]
    AccessToken = 4
}
