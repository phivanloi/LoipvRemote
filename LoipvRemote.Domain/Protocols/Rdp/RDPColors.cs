using LoipvRemote.Domain.Metadata;

namespace LoipvRemote.Domain.Protocols.Rdp;

public enum RDPColors
{
    [ProtocolDisplayKey("Rdp256Colors")]
    Colors256 = 8,

    [ProtocolDisplayKey("Rdp32768Colors")]
    Colors15Bit = 15,

    [ProtocolDisplayKey("Rdp65536Colors")]
    Colors16Bit = 16,

    [ProtocolDisplayKey("Rdp16777216Colors")]
    Colors24Bit = 24,

    [ProtocolDisplayKey("Rdp4294967296Colors")]
    Colors32Bit = 32
}
