using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LoipvRemote.Infrastructure.Windows.WindowEmbedding;

/// <summary>Enumerates top-level and child Win32 window handles.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsHandleEnumerator
{
    public IReadOnlyList<IntPtr> EnumerateTopLevelWindows() =>
        Enumerate(callback => NativeEnumWindows(callback, IntPtr.Zero));

    public IReadOnlyList<IntPtr> EnumerateChildWindows(IntPtr parentWindowHandle) =>
        Enumerate(callback => NativeEnumChildWindows(parentWindowHandle, callback, IntPtr.Zero));

    private static IReadOnlyList<IntPtr> Enumerate(Func<WindowEnumProc, bool> enumerate)
    {
        var handles = new List<IntPtr>();
        WindowEnumProc callback = (windowHandle, _) => { handles.Add(windowHandle); return true; };
        enumerate(callback);
        GC.KeepAlive(callback);
        return handles;
    }

    private delegate bool WindowEnumProc(IntPtr windowHandle, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool NativeEnumWindows(WindowEnumProc callback, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool NativeEnumChildWindows(IntPtr parentWindowHandle, WindowEnumProc callback, IntPtr parameter);
}
