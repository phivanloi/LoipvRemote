using System.Runtime.InteropServices;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Infrastructure.Windows.WindowEmbedding;

/// <summary>
/// Owns a lightweight native child HWND used as the parent of a foreign protocol
/// window. This is deliberately independent of the desktop UI framework.
/// </summary>
public sealed class WindowsChildWindowHost : IDisposable
{
    private const int WsChild = unchecked((int)0x40000000);
    private const int WsVisible = unchecked((int)0x10000000);
    private const int WsClipChildren = unchecked((int)0x02000000);
    private const int WsClipSiblings = unchecked((int)0x04000000);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private const uint SwpNoSendChanging = 0x0400;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;

    public WindowsChildWindowHost(IntPtr parentWindowHandle)
    {
        if (parentWindowHandle == IntPtr.Zero)
            throw new ArgumentException("A non-zero parent window handle is required.", nameof(parentWindowHandle));

        Handle = CreateWindowEx(
            0,
            "STATIC",
            string.Empty,
            WsChild | WsVisible | WsClipChildren | WsClipSiblings,
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
    }

    /// <summary>Shows or hides one protocol child without changing the session host itself.</summary>
    public void SetChildVisible(IntPtr childWindowHandle, bool visible)
    {
        ObjectDisposedException.ThrowIf(Handle == IntPtr.Zero, this);
        if (childWindowHandle != IntPtr.Zero)
            _ = ShowWindow(childWindowHandle, visible ? SwShowNoActivate : SwHide);
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
}
