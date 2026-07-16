using LoipvRemote.Infrastructure.Windows.Interop;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LoipvRemote.Infrastructure.Windows.WindowMessages;

/// <summary>Shell-level window messages exposed without leaking the Win32 P/Invoke surface.</summary>
[SupportedOSPlatform("windows")]
public static class WindowsShellWindowMessages
{
    public const int MkLeftButton = NativeMethods.MK_LBUTTON;
    public const int SwpNoActivate = NativeMethods.SWP_NOACTIVATE;
    public const int WaClickActive = NativeMethods.WA_CLICKACTIVE;
    public const int WmActivate = NativeMethods.WM_ACTIVATE;
    public const int WmActivateApp = NativeMethods.WM_ACTIVATEAPP;
    public const int WmChangeClipboardChain = NativeMethods.WM_CHANGECBCHAIN;
    public const int WmDrawClipboard = NativeMethods.WM_DRAWCLIPBOARD;
    public const int WmLButtonDown = NativeMethods.WM_LBUTTONDOWN;
    public const int WmMouseActivate = NativeMethods.WM_MOUSEACTIVATE;
    public const int WmNcHitTest = NativeMethods.WM_NCHITTEST;
    public const int WmNcLButtonDown = 0x00A1;
    public const int WmNcLButtonDoubleClick = NativeMethods.WM_NCLBUTTONDBLCLK;
    public const int WmSysCommand = NativeMethods.WM_SYSCOMMAND;
    public const int WmWindowPositionChanged = NativeMethods.WM_WINDOWPOSCHANGED;
    public const int TabControlAdjustRect = NativeMethods.TCM_ADJUSTRECT;

    public static nint SetClipboardViewer(nint windowHandle) => NativeMethods.SetClipboardViewer(windowHandle);

    public static bool ChangeClipboardChain(nint windowHandle, nint nextWindowHandle) =>
        NativeMethods.ChangeClipboardChain(windowHandle, nextWindowHandle);

    public static bool ReleaseCapture() => NativeMethods.ReleaseCapture();

    public static nint SendMessage(nint windowHandle, int message, nint wParam, nint lParam) =>
        NativeMethods.SendMessage(windowHandle, message, wParam, lParam);

    public static nint WindowFromPoint(Point point) => NativeMethods.WindowFromPoint(point);

    public static WindowPosition ReadWindowPosition(nint address)
    {
        NativeMethods.WINDOWPOS position = NativeMethods.ReadWindowPosition(address);
        return new WindowPosition(position.hwnd, position.hwndInsertAfter, position.x, position.y,
            position.cx, position.cy, position.flags);
    }

    public static int LowWord(nint value) => NativeMethods.LOWORD(value);

    public static int MakeLParam(ref int low, ref int high) => NativeMethods.MAKELPARAM(ref low, ref high);

    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct WindowPosition(
        nint WindowHandle,
        nint InsertAfter,
        int X,
        int Y,
        int Width,
        int Height,
        int Flags);
}
