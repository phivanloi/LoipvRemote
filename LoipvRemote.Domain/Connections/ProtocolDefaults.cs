namespace LoipvRemote.Domain.Connections;

/// <summary>
/// Stable protocol defaults used by connection definitions and all hosts.
/// Keeping these values in Domain prevents the desktop shell from owning
/// protocol policy.
/// </summary>
public static class ProtocolDefaults
{
    public static int GetDefaultPort(ProtocolKind protocol) => protocol switch
    {
        ProtocolKind.Ssh2 => 22,
        ProtocolKind.Rdp => 3389,
        ProtocolKind.Vnc => 5900,
        _ => 0
    };
}
