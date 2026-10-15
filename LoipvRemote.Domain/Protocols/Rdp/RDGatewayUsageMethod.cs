using LoipvRemote.Domain.Metadata;

namespace LoipvRemote.Domain.Protocols.Rdp;

public enum RDGatewayUsageMethod
{
    [ProtocolDisplayKey("Never")]
    Never = 0,

    [ProtocolDisplayKey("Always")]
    Always = 1,

    [ProtocolDisplayKey("Detect")]
    Detect = 2
}
