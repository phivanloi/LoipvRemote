using LoipvRemote.Domain.Metadata;

namespace LoipvRemote.Domain.Protocols.Rdp;

public enum RDPDiskDrives
{
    [ProtocolDisplayKey("RdpDrivesNone")]
    None,

    [ProtocolDisplayKey("RdpDrivesLocal")]
    Local,

    [ProtocolDisplayKey("RdpDrivesAll")]
    All,

    [ProtocolDisplayKey("RdpDrivesCustom")]
    Custom
}
