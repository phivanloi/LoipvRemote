using LoipvRemote.Infrastructure.Windows.Interop;
using System.Runtime.Versioning;

namespace LoipvRemote.Infrastructure.Windows.ApplicationIdentity;

[SupportedOSPlatform("windows")]
public sealed class WindowsApplicationIdentityService : IApplicationIdentityService
{
    public void Configure(string appUserModelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appUserModelId);
        _ = NativeMethods.SetCurrentProcessExplicitAppUserModelID(appUserModelId);
    }
}
