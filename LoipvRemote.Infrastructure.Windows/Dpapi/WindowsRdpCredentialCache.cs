using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LoipvRemote.Infrastructure.Windows.Dpapi;

public enum ClearWindowsCredentialResult { Deleted, NotFound, Failed }

/// <summary>Deletes the current user's cached TERMSRV credential via Windows Credential Manager.</summary>
[SupportedOSPlatform("windows")]
public static class WindowsRdpCredentialCache
{
    public static ClearWindowsCredentialResult Clear(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            return ClearWindowsCredentialResult.Failed;

        if (CredDelete($"TERMSRV/{hostname}", CredentialType.DomainPassword, 0))
            return ClearWindowsCredentialResult.Deleted;

        return Marshal.GetLastWin32Error() == ErrorNotFound
            ? ClearWindowsCredentialResult.NotFound
            : ClearWindowsCredentialResult.Failed;
    }

    private const int ErrorNotFound = 1168;
    private enum CredentialType : uint { DomainPassword = 2 }

    [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string target, CredentialType type, int reservedFlag);
}
