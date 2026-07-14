namespace LoipvRemote.Protocols.Rdp;

/// <summary>Prevents clear-text credentials when RDP security features forbid them.</summary>
public static class RdpCredentialPolicy
{
    public static bool ShouldAssignClearTextPassword(bool useRestrictedAdmin, bool useRemoteCredentialGuard) =>
        !useRestrictedAdmin && !useRemoteCredentialGuard;
}
