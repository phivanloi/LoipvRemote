namespace LoipvRemote.Domain.Protocols.Rdp;

/// <summary>Supported RDP ActiveX client generations persisted with a connection definition.</summary>
public enum RdpVersion
{
    Rdc6,
    Rdc7,
    Rdc8,
    Rdc9,
    Rdc10,
    Rdc11,
    Highest = 1000
}
