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

    public IntPtr FindChildWindow(IntPtr parentHandle) =>
        NativeMethods.FindWindowEx(parentHandle, IntPtr.Zero, null, null);

    public bool HasClassName(IntPtr windowHandle, string className)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);

        StringBuilder actualClassName = new(256);
        NativeMethods.GetClassName(windowHandle, actualClassName, actualClassName.Capacity);
        return actualClassName.ToString().Equals(className, StringComparison.OrdinalIgnoreCase);
    }

    public void Hide(IntPtr windowHandle) => NativeMethods.ShowWindow(windowHandle, unchecked((int)NativeMethods.SW_HIDE));

    public void Restore(IntPtr windowHandle) => NativeMethods.ShowWindow(windowHandle, unchecked((int)NativeMethods.SW_RESTORE));

    public void SetParent(IntPtr childHandle, IntPtr parentHandle) =>
        NativeMethods.SetParent(childHandle, parentHandle);

    public int GetWindowStyle(IntPtr windowHandle) =>
        NativeMethods.GetWindowLong(windowHandle, NativeMethods.GWL_STYLE);

    public bool TrySetWindowStyle(IntPtr windowHandle, int style)
    {
        int previousStyle = NativeMethods.SetWindowLong(windowHandle, NativeMethods.GWL_STYLE, style);
        return previousStyle != 0;
    }

    public void RefreshFrame(IntPtr windowHandle) =>
        NativeMethods.SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);

    public void ForwardInputLanguageChange(IntPtr windowHandle) =>
        NativeMethods.SendMessage(windowHandle,
            (uint)NativeMethods.WM_INPUTLANGCHANGE,
            IntPtr.Zero,
            NativeMethods.GetKeyboardLayout(0));

    public void SendMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam) =>
        NativeMethods.SendMessage(windowHandle, message, wParam, lParam);

    public void Move(IntPtr windowHandle, int x, int y, int width, int height) =>
        NativeMethods.MoveWindow(windowHandle, x, y, width, height, true);

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
