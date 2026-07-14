using LoipvRemote.Infrastructure.Windows.Interop;

namespace LoipvRemote.Infrastructure.Windows.WindowEmbedding;

/// <summary>Creates the Win32-backed input-focus controller for embedded windows.</summary>
public static class WindowsEmbeddedWindowFocusControllerFactory
{
    public static EmbeddedWindowFocusController Create() => new(
        NativeMethods.GetWindowThreadProcessId,
        NativeMethods.AttachThreadInput,
        NativeMethods.SetFocus);
}
