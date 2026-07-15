using LoipvRemote.Infrastructure.Windows.Interop;
using System.Runtime.Versioning;

namespace LoipvRemote.Infrastructure.Windows.WindowTheme;

[SupportedOSPlatform("windows")]
public static class WindowsWindowThemeService
{
    public static bool ApplyTitleBar(nint windowHandle, bool dark)
    {
        if (windowHandle == nint.Zero)
            return false;

        return NativeMethods.UseImmersiveDarkMode(windowHandle, dark);
    }
}
