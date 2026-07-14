using LoipvRemote.Domain.Metadata;

namespace LoipvRemote.Domain.Protocols.Rdp;

public enum RDPSounds
{
    [ProtocolDisplayKey("RdpSoundBringToThisComputer")]
    BringToThisComputer = 0,

    [ProtocolDisplayKey("RdpSoundLeaveAtRemoteComputer")]
    LeaveAtRemoteComputer = 1,

    [ProtocolDisplayKey("DoNotPlay")]
    DoNotPlay = 2
}
