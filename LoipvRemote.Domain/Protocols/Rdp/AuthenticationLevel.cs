using LoipvRemote.Domain.Metadata;

namespace LoipvRemote.Domain.Protocols.Rdp;

/// <summary>Server authentication behavior selected for an RDP connection definition.</summary>
public enum AuthenticationLevel
{
    [ProtocolDisplayKey("AlwaysConnectEvenIfAuthFails")]
    NoAuth = 0,

    [ProtocolDisplayKey("DontConnectWhenAuthFails")]
    AuthRequired = 1,

    [ProtocolDisplayKey("WarnIfAuthFails")]
    WarnOnFailedAuth = 2
}
