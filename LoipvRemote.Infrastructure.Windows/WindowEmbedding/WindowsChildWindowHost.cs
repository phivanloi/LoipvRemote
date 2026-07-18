using System.Runtime.InteropServices;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Infrastructure.Windows.WindowEmbedding;

/// <summary>
/// Owns a borderless native popup used as the parent of a foreign protocol
/// window. The popup is owned by the application window, but is not its child:
/// this keeps foreign HWND content above WinUI's DirectComposition surface.
/// </summary>
public sealed class WindowsChildWindowHost : IDisposable
{
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsVisible = unchecked((int)0x10000000);
    private const int WsClipChildren = unchecked((int)0x02000000);
    private const int WsClipSiblings = unchecked((int)0x04000000);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private const uint SwpNoSendChanging = 0x0400;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpShowWindow = 0x0040;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;

    public WindowsChildWindowHost(IntPtr parentWindowHandle)
    {
        if (parentWindowHandle == IntPtr.Zero)
            throw new ArgumentException("A non-zero parent window handle is required.", nameof(parentWindowHandle));

        // A child HWND is always below WinUI's DirectComposition surface. An
        // owned popup stays visually above it while Windows keeps it tied to
        // the application's lifetime and Z-order.
        Handle = CreateWindowEx(
            0,
            "STATIC",
            string.Empty,
            WsPopup | WsClipChildren | WsClipSiblings,
            0,
            0,
            1,
            1,
            parentWindowHandle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        if (Handle == IntPtr.Zero)
            throw new InvalidOperationException($"Could not create the embedded session host window (Win32 error {Marshal.GetLastWin32Error()}).");
    }

    public IntPtr Handle { get; private set; }

    public void Resize(EmbeddedWindowBounds bounds)
    {
        ObjectDisposedException.ThrowIf(Handle == IntPtr.Zero, this);
        if (!bounds.IsValid)
            return;

        if (!SetWindowPos(
                Handle,
                IntPtr.Zero,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                SwpNoActivate | SwpNoOwnerZOrder | SwpNoSendChanging))
        {
            throw new InvalidOperationException($"Could not resize the embedded session host window (Win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    public void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(Handle == IntPtr.Zero, this);
        _ = ShowWindow(Handle, visible ? SwShowNoActivate : SwHide);
        if (visible)
            BringToFront();
    }

    /// <summary>Activates the owned popup before focus is transferred to its protocol child.</summary>
    public void Activate()
    {
        ObjectDisposedException.ThrowIf(Handle == IntPtr.Zero, this);
        _ = SetForegroundWindow(Handle);
        _ = SetFocus(Handle);
    }

    /// <summary>Shows or hides one protocol child without changing the session host itself.</summary>
    public void SetChildVisible(IntPtr childWindowHandle, bool visible)
    {
        ObjectDisposedException.ThrowIf(Handle == IntPtr.Zero, this);
        if (childWindowHandle != IntPtr.Zero)
        {
            _ = ShowWindow(childWindowHandle, visible ? SwShowNoActivate : SwHide);
            if (visible)
                BringChildToFront(childWindowHandle);
        }
    }

    /// <summary>Keeps the native host above the WinUI composition surface.</summary>
    public void BringToFront(bool ensureVisible = true)
    {
        ObjectDisposedException.ThrowIf(Handle == IntPtr.Zero, this);
        if (!SetWindowPos(
                Handle,
                IntPtr.Zero, // HWND_TOP
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder |
                (ensureVisible ? SwpShowWindow : 0) | SwpNoSendChanging))
        {
            throw new InvalidOperationException($"Could not bring the embedded session host to the front (Win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    private static void BringChildToFront(IntPtr childWindowHandle)
    {
        if (!SetWindowPos(
                childWindowHandle,
                IntPtr.Zero, // HWND_TOP
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder | SwpShowWindow | SwpNoSendChanging))
        {
            throw new InvalidOperationException($"Could not bring the embedded protocol window to the front (Win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero)
            return;

        _ = DestroyWindow(Handle);
        Handle = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int extendedStyle,
        string className,
        string windowName,
        int style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parentWindowHandle,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetFocus(IntPtr windowHandle);
}
