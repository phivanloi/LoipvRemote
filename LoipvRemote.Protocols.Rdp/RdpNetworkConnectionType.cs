namespace LoipvRemote.Protocols.Rdp;

/// <summary>Network profiles exposed by the RDP ActiveX client.</summary>
public enum RdpNetworkConnectionType
{
    Modem = 1,
    BroadbandLow = 2,
    Satellite = 3,
    BroadbandHigh = 4,
    Wan = 5,
    Lan = 6,
    AutoDetect = 7
}
