using LoipvRemote.Domain.Metadata;

namespace LoipvRemote.Domain.Protocols.Rdp;

public enum RDPSoundQuality
{
    [ProtocolDisplayKey("Dynamic")]
    Dynamic = 0,

    [ProtocolDisplayKey("Medium")]
    Medium = 1,

    [ProtocolDisplayKey("High")]
    High = 2
}
