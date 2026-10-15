namespace LoipvRemote.Domain.Connections;

public static class ProtocolPolicy
{
    public static bool AllowsBlankHostname(ProtocolKind protocol) => false;
}
