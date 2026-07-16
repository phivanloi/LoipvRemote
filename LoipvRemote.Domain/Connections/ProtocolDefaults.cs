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
        ProtocolKind.Rdp => 3389,
        ProtocolKind.Vnc or ProtocolKind.Ard => 5900,
        ProtocolKind.Ssh1 or ProtocolKind.Ssh2 => 22,
        ProtocolKind.Telnet or ProtocolKind.Raw => 23,
        ProtocolKind.Rlogin => 513,
        ProtocolKind.Http => 80,
        ProtocolKind.Https => 443,
        ProtocolKind.PowerShell => 5985,
        ProtocolKind.Terminal or ProtocolKind.Wsl or ProtocolKind.ExternalApplication or ProtocolKind.AnyDesk => 0,
        _ => 0
    };
}
