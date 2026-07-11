namespace LoipvRemote.Connection.Protocol.RDP
{
    internal static class RdpCredentialPolicy
    {
        internal static bool ShouldAssignClearTextPassword(bool useRestrictedAdmin, bool useRemoteCredentialGuard)
        {
            return !useRestrictedAdmin && !useRemoteCredentialGuard;
        }
    }
}
