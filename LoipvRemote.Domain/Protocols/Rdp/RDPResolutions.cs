using LoipvRemote.Domain.Metadata;

namespace LoipvRemote.Domain.Protocols.Rdp;

public enum RDPResolutions
{
    [ProtocolDisplayKey("SmartSize")]
    SmartSize,

    [ProtocolDisplayKey("FitToPanel")]
    FitToWindow,

    [ProtocolDisplayKey("Fullscreen")]
    Fullscreen
}
