using System.Runtime.InteropServices;
using System.Text;
using LoipvRemote.Infrastructure.Windows.Interop;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Infrastructure.Windows.WindowEmbedding;

/// <summary>Owns the Win32 operations required by embedded third-party windows.</summary>
public sealed class WindowsEmbeddedWindowOperations : IEmbeddedWindowOperations
{
    private readonly EmbeddedWindowFocusController _focusController =
        WindowsEmbeddedWindowFocusControllerFactory.Create();

    public bool IsForegroundWindow(IntPtr windowHandle) =>
        NativeMethods.GetForegroundWindow() == windowHandle;

    public IntPtr FindChildWindow(IntPtr parentHandle, IntPtr afterHandle = default) =>
        NativeMethods.FindWindowEx(parentHandle, afterHandle, null, null);

    public uint GetWindowProcessId(IntPtr windowHandle)
    {
        _ = NativeMethods.GetWindowThreadProcessId(windowHandle, out uint processId);
        return processId;
    }

    public bool HasClassName(IntPtr windowHandle, string className)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);

        StringBuilder actualClassName = new(256);
        int copiedCharacters = NativeMethods.GetClassName(windowHandle, actualClassName, actualClassName.Capacity);
        if (copiedCharacters <= 0)
            return false;
        return actualClassName.ToString().Equals(className, StringComparison.OrdinalIgnoreCase);
    }

    public void Hide(IntPtr windowHandle) => _ = NativeMethods.ShowWindow(windowHandle, unchecked((int)NativeMethods.SW_HIDE));

    public void Show(IntPtr windowHandle) => _ = NativeMethods.ShowWindow(windowHandle, unchecked((int)NativeMethods.SW_SHOW));

    public void Restore(IntPtr windowHandle) => _ = NativeMethods.ShowWindow(windowHandle, unchecked((int)NativeMethods.SW_RESTORE));

    public void SetParent(IntPtr childHandle, IntPtr parentHandle)
    {
        if (childHandle == IntPtr.Zero)
            throw new ArgumentException("A non-zero child window handle is required.", nameof(childHandle));
        if (parentHandle == IntPtr.Zero)
            throw new ArgumentException("A non-zero parent window handle is required.", nameof(parentHandle));

        // SetParent returns zero both when the old parent was the desktop and
        // when the operation failed. Clear/read the last error and then verify
        // the resulting parent so callers cannot report a connected session
        // while the protocol HWND remains detached.
        Marshal.SetLastPInvokeError(0);
        _ = NativeMethods.SetParent(childHandle, parentHandle);
        int error = Marshal.GetLastPInvokeError();
        if (error != 0)
            throw new InvalidOperationException($"Could not attach the protocol window to its host (Win32 error {error}).");
        if (NativeMethods.GetParent(childHandle) != parentHandle)
            throw new InvalidOperationException("Windows did not attach the protocol window to its requested host.");
    }

    public int GetWindowStyle(IntPtr windowHandle) =>
        NativeMethods.GetWindowLong(windowHandle, NativeMethods.GWL_STYLE);

    public bool TrySetWindowStyle(IntPtr windowHandle, int style)
    {
        IntPtr previousStyle = NativeMethods.SetWindowLongPtr(
            windowHandle,
            NativeMethods.GWL_STYLE,
            new IntPtr(style));
        return previousStyle != IntPtr.Zero;
    }

    public int GetWindowExtendedStyle(IntPtr windowHandle) =>
        NativeMethods.GetWindowLong(windowHandle, NativeMethods.GWL_EXSTYLE);

    public bool TrySetWindowExtendedStyle(IntPtr windowHandle, int style)
    {
        IntPtr previousStyle = NativeMethods.SetWindowLongPtr(
            windowHandle,
            NativeMethods.GWL_EXSTYLE,
            new IntPtr(style));
        return previousStyle != IntPtr.Zero;
    }

    public void RefreshFrame(IntPtr windowHandle) =>
        NativeMethods.SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_FRAMECHANGED |
            NativeMethods.SWP_SHOWWINDOW);

    public void ForwardInputLanguageChange(IntPtr windowHandle) =>
        NativeMethods.SendMessage(windowHandle,
            (uint)NativeMethods.WM_INPUTLANGCHANGE,
            IntPtr.Zero,
            NativeMethods.GetKeyboardLayout(0));

    public void SendMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam) =>
        NativeMethods.SendMessage(windowHandle, message, wParam, lParam);

    public void Move(IntPtr windowHandle, int x, int y, int width, int height) =>
        NativeMethods.SetWindowPos(windowHandle, IntPtr.Zero, x, y, width, height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_ASYNCWINDOWPOS | NativeMethods.SWP_SHOWWINDOW);

    public void ShowSettingsDialog(IntPtr windowHandle, int commandId)
    {
        NativeMethods.PostMessage(windowHandle, NativeMethods.WM_SYSCOMMAND, (IntPtr)commandId, IntPtr.Zero);
        NativeMethods.SetForegroundWindow(windowHandle);
    }

    public void Activate(IntPtr windowHandle) => NativeMethods.SetForegroundWindow(windowHandle);

    public void SetFocus(IntPtr windowHandle) => NativeMethods.SetFocus(windowHandle);

    public bool TryFocus(IntPtr ownerWindowHandle, IntPtr embeddedWindowHandle) =>
        _focusController.TryFocus(ownerWindowHandle, embeddedWindowHandle);

    public void Close(IntPtr windowHandle) =>
        NativeMethods.SendMessage(windowHandle, 0x0010, IntPtr.Zero, IntPtr.Zero);
}
