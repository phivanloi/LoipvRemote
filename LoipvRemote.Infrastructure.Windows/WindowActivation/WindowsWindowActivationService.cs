using System.Runtime.Versioning;
using LoipvRemote.Infrastructure.Windows.Interop;

namespace LoipvRemote.Infrastructure.Windows.WindowActivation;

[SupportedOSPlatform("windows")]
public sealed class WindowsWindowActivationService : IWindowActivationService
{
    public bool Activate(nint windowHandle)
    {
        ValidateHandle(windowHandle);
        return NativeMethods.SetForegroundWindow(windowHandle);
    }

    public bool RestoreAndActivate(nint windowHandle)
    {
        ValidateHandle(windowHandle);
        if (NativeMethods.IsIconic(windowHandle) != 0)
            _ = NativeMethods.ShowWindow(windowHandle, (int)NativeMethods.SW_RESTORE);

        return NativeMethods.SetForegroundWindow(windowHandle);
    }

    private static void ValidateHandle(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
            throw new ArgumentOutOfRangeException(nameof(windowHandle));
    }
}
