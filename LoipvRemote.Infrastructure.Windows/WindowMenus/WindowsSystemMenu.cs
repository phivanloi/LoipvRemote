using LoipvRemote.Infrastructure.Windows.Interop;
using Microsoft.Win32.SafeHandles;
using System.Drawing;
using System.Runtime.Versioning;

namespace LoipvRemote.Infrastructure.Windows.WindowMenus;

[SupportedOSPlatform("windows")]
public sealed class WindowsSystemMenu : SafeHandleZeroOrMinusOneIsInvalid
{
    [Flags]
    public enum MenuOptions
    {
        Text = NativeMethods.MF_STRING,
        Separator = NativeMethods.MF_SEPARATOR,
        ByPosition = NativeMethods.MF_BYPOSITION,
        Popup = NativeMethods.MF_POPUP,
        SystemCommand = NativeMethods.WM_SYSCOMMAND
    }

    private readonly nint _formHandle;
    private bool _disposed;

    public WindowsSystemMenu(nint formHandle) : base(true)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(formHandle, nint.Zero);

        _formHandle = formHandle;
        SetHandle(NativeMethods.GetSystemMenu(_formHandle, false));
    }

    public nint SystemMenuHandle => handle;

    public void Reset() => SetHandle(NativeMethods.GetSystemMenu(_formHandle, true));

    public static void AppendMenuItem(nint parentMenu, MenuOptions flags, nint id, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        NativeMethods.AppendMenu(parentMenu, (int)flags, id, text);
    }

    public static nint CreatePopupMenuItem() => NativeMethods.CreatePopupMenu();

    public static bool InsertMenuItem(nint systemMenu, int position, MenuOptions flags, nint subMenu, string? text)
    {
        return NativeMethods.InsertMenu(systemMenu, position, (int)flags, subMenu, text ?? string.Empty);
    }

    public static nint SetBitmap(nint menu, int position, MenuOptions flags, Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        nint bitmapHandle = bitmap.GetHbitmap();
        return new nint(Convert.ToInt32(NativeMethods.SetMenuItemBitmaps(menu, position, (int)flags,
            bitmapHandle, bitmapHandle)));
    }

    protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        base.Dispose(disposing);
        _disposed = true;
    }
}
