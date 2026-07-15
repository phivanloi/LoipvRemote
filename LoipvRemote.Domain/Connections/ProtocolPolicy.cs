namespace LoipvRemote.Domain.Connections;

public static class ProtocolPolicy
{
    public static bool AllowsBlankHostname(ProtocolKind protocol) => protocol is
        ProtocolKind.ExternalApplication or
        ProtocolKind.PowerShell or
        ProtocolKind.Wsl or
        ProtocolKind.Terminal;
}
