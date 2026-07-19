using System.Runtime.InteropServices;

namespace LoipvRemote.Infrastructure.Windows.WindowEmbedding;

/// <summary>
/// Routes middle-clicks in the custom title bar to the WinUI tab strip. A
/// low-level hook is required because SetTitleBar regions can be handled by the
/// compositor without producing XAML or ordinary HWND pointer messages.
/// </summary>
public sealed class WindowSessionPointerController : IDisposable
{
    private const int WhMouseLl = 14;
    private const uint WmMiddleButtonDown = 0x0207;
    private static readonly object SyncRoot = new();
    private static readonly MouseHookProcedure Callback = MouseProcedure;
    private static WindowSessionPointerController? _current;

    private readonly IntPtr _windowHandle;
    private readonly Func<int, int, bool> _middleClick;
    private IntPtr _hookHandle;
    private bool _disposed;

    public WindowSessionPointerController(IntPtr windowHandle, Func<int, int, bool> middleClick)
    {
        if (windowHandle == IntPtr.Zero)
            throw new ArgumentException("A top-level window handle is required.", nameof(windowHandle));

        _middleClick = middleClick ?? throw new ArgumentNullException(nameof(middleClick));
        _windowHandle = windowHandle;
        lock (SyncRoot)
        {
            if (_current is not null)
                throw new InvalidOperationException("A session pointer hook is already installed.");

            _current = this;
            _hookHandle = SetWindowsHookEx(WhMouseLl, Callback, IntPtr.Zero, 0);
            if (_hookHandle == IntPtr.Zero)
            {
                _current = null;
                throw new InvalidOperationException(
                    $"Could not install session pointer handling (Win32 error {Marshal.GetLastWin32Error()}).");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        lock (SyncRoot)
        {
            if (_hookHandle != IntPtr.Zero)
            {
                _ = UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            if (ReferenceEquals(_current, this))
                _current = null;
        }

        GC.SuppressFinalize(this);
    }

    private static IntPtr MouseProcedure(int code, IntPtr wParam, IntPtr lParam)
    {
        WindowSessionPointerController? controller = _current;
        if (code < 0 || controller is null || controller._disposed ||
            wParam != (IntPtr)WmMiddleButtonDown || lParam == IntPtr.Zero)
        {
            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        try
        {
            MouseHookData data = Marshal.PtrToStructure<MouseHookData>(lParam);
            IntPtr windowAtPoint = WindowFromPoint(data.Point);
            if (!WindowSessionHotKeyController.IsDescendantWindow(
                    controller._windowHandle,
                    windowAtPoint,
                    GetParent))
            {
                return CallNextHookEx(controller._hookHandle, code, wParam, lParam);
            }

            Point clientPoint = data.Point;
            if (!ScreenToClient(controller._windowHandle, ref clientPoint))
                return CallNextHookEx(controller._hookHandle, code, wParam, lParam);

            bool handled = controller._middleClick(clientPoint.X, clientPoint.Y);
            if (ShouldSuppressMiddleClick(handled))
                return (IntPtr)1;
        }
        catch
        {
            // A hook must never block unrelated pointer input.
        }

        return CallNextHookEx(controller._hookHandle, code, wParam, lParam);
    }

    internal static bool ShouldSuppressMiddleClick(bool handled) => handled;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookId,
        MouseHookProcedure procedure,
        IntPtr moduleHandle,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(IntPtr windowHandle, ref Point point);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr MouseHookProcedure(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public Point Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
