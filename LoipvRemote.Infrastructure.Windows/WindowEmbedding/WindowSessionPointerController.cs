using System.Runtime.InteropServices;

namespace LoipvRemote.Infrastructure.Windows.WindowEmbedding;

/// <summary>
/// Routes pointer gestures that ordinary XAML events cannot observe. This
/// includes middle-clicks in the custom title bar and primary clicks inside a
/// cross-process embedded focus-target HWND.
/// </summary>
public sealed class WindowSessionPointerController : IDisposable
{
    private const int WhMouseLl = 14;
    private const uint WmLeftButtonDown = 0x0201;
    private const uint WmMiddleButtonDown = 0x0207;
    private static readonly object SyncRoot = new();
    private static readonly MouseHookProcedure Callback = MouseProcedure;
    private static WindowSessionPointerController? _current;

    private readonly IntPtr _windowHandle;
    private readonly Func<int, int, bool> _middleClick;
    private readonly Func<IntPtr> _embeddedSessionFocusTargetHandle;
    private readonly Action _embeddedSessionPrimaryClick;
    private readonly Action _embeddedSessionOutsidePrimaryClick;
    private IntPtr _hookHandle;
    private bool _disposed;

    public WindowSessionPointerController(
        IntPtr windowHandle,
        Func<int, int, bool> middleClick,
        Func<IntPtr> embeddedSessionFocusTargetHandle,
        Action embeddedSessionPrimaryClick,
        Action embeddedSessionOutsidePrimaryClick)
    {
        if (windowHandle == IntPtr.Zero)
            throw new ArgumentException("A top-level window handle is required.", nameof(windowHandle));

        _middleClick = middleClick ?? throw new ArgumentNullException(nameof(middleClick));
        _embeddedSessionFocusTargetHandle = embeddedSessionFocusTargetHandle ??
            throw new ArgumentNullException(nameof(embeddedSessionFocusTargetHandle));
        _embeddedSessionPrimaryClick = embeddedSessionPrimaryClick ??
            throw new ArgumentNullException(nameof(embeddedSessionPrimaryClick));
        _embeddedSessionOutsidePrimaryClick = embeddedSessionOutsidePrimaryClick ??
            throw new ArgumentNullException(nameof(embeddedSessionOutsidePrimaryClick));
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
        bool primaryButtonDown = wParam == (IntPtr)WmLeftButtonDown;
        bool middleButtonDown = wParam == (IntPtr)WmMiddleButtonDown;
        if (code < 0 || controller is null || controller._disposed ||
            (!primaryButtonDown && !middleButtonDown) || lParam == IntPtr.Zero)
        {
            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        try
        {
            MouseHookData data = Marshal.PtrToStructure<MouseHookData>(lParam);
            IntPtr windowAtPoint = WindowFromPoint(data.Point);
            if (primaryButtonDown)
            {
                IntPtr focusTarget = controller._embeddedSessionFocusTargetHandle();
                bool insideEmbeddedSession = IsInsideEmbeddedFocusTarget(
                    focusTarget,
                    windowAtPoint,
                    GetParent);
                if (ShouldRestoreEmbeddedFocus(primaryButtonDown, insideEmbeddedSession))
                    controller._embeddedSessionPrimaryClick();
                else
                {
                    bool insideOwnerWindow = WindowSessionHotKeyController.IsDescendantWindow(
                        controller._windowHandle,
                        windowAtPoint,
                        GetParent);
                    if (ShouldCancelEmbeddedFocusRestore(
                            primaryButtonDown,
                            insideOwnerWindow,
                            insideEmbeddedSession))
                    {
                        controller._embeddedSessionOutsidePrimaryClick();
                    }
                }

                // Focus recovery augments the native click; it must never
                // consume terminal selection, cursor placement, or RDP input.
                return CallNextHookEx(controller._hookHandle, code, wParam, lParam);
            }

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

    internal static bool ShouldRestoreEmbeddedFocus(
        bool primaryButtonDown,
        bool insideEmbeddedSession) =>
        primaryButtonDown && insideEmbeddedSession;

    internal static bool ShouldCancelEmbeddedFocusRestore(
        bool primaryButtonDown,
        bool insideOwnerWindow,
        bool insideEmbeddedFocusTarget) =>
        primaryButtonDown && insideOwnerWindow && !insideEmbeddedFocusTarget;

    internal static bool IsInsideEmbeddedFocusTarget(
        IntPtr focusTargetWindowHandle,
        IntPtr windowAtPoint,
        Func<IntPtr, IntPtr> getParent) =>
        focusTargetWindowHandle != IntPtr.Zero &&
        windowAtPoint != IntPtr.Zero &&
        WindowSessionHotKeyController.IsDescendantWindow(
            focusTargetWindowHandle,
            windowAtPoint,
            getParent);

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
