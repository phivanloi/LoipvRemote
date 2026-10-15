using System.Runtime.InteropServices;

namespace LoipvRemote.Infrastructure.Windows.WindowEmbedding;

/// <summary>
/// Routes Ctrl+Tab shortcuts to the WinUI session strip even while an embedded
/// third-party child process owns keyboard focus. Keys for other applications
/// are always passed through unchanged.
/// </summary>
public sealed class WindowSessionHotKeyController : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint VkTab = 0x09;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private static readonly object SyncRoot = new();
    private static readonly KeyboardHookProcedure Callback = KeyboardProcedure;
    private static WindowSessionHotKeyController? _current;

    private readonly IntPtr _windowHandle;
    private readonly Action<int> _navigate;
    private readonly Action<string>? _diagnostics;
    private readonly Action? _recoverFocus;
    private IntPtr _hookHandle;
    private bool _enabled;
    private bool _tabChordActive;
    private int _pendingDirection;
    private bool _focusRecoveryPending;
    private bool _disposed;

    public WindowSessionHotKeyController(
        IntPtr windowHandle,
        Action<int> navigate,
        Action<string>? diagnostics = null,
        Action? recoverFocus = null)
    {
        if (windowHandle == IntPtr.Zero)
            throw new ArgumentException("A top-level window handle is required.", nameof(windowHandle));

        _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
        _diagnostics = diagnostics;
        _recoverFocus = recoverFocus;
        _windowHandle = windowHandle;
        lock (SyncRoot)
        {
            if (_current is not null)
                throw new InvalidOperationException("A session keyboard hook is already installed.");

            _current = this;
            _hookHandle = SetWindowsHookEx(WhKeyboardLl, Callback, IntPtr.Zero, 0);
            if (_hookHandle == IntPtr.Zero)
            {
                _current = null;
                throw new InvalidOperationException($"Could not install session keyboard shortcuts (Win32 error {Marshal.GetLastWin32Error()}).");
            }

            WriteDiagnostics("session-keyboard-hook-installed");
        }
    }

    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _enabled = enabled;
        if (!enabled)
        {
            _tabChordActive = false;
            _pendingDirection = 0;
            _focusRecoveryPending = false;
        }
    }

    public bool HasForegroundOwnership()
    {
        IntPtr foregroundWindow = GetForegroundWindow();
        return OwnsForegroundWindow(_windowHandle, foregroundWindow, IsDescendantWindow);
    }

    internal static bool OwnsForegroundWindow(
        IntPtr ownerWindowHandle,
        IntPtr foregroundWindowHandle,
        Func<IntPtr, IntPtr, bool> isChild)
    {
        ArgumentNullException.ThrowIfNull(isChild);
        return ownerWindowHandle != IntPtr.Zero &&
            (foregroundWindowHandle == ownerWindowHandle ||
             foregroundWindowHandle != IntPtr.Zero && isChild(ownerWindowHandle, foregroundWindowHandle));
    }

    internal static bool IsDescendantWindow(
        IntPtr ownerWindowHandle,
        IntPtr candidateWindowHandle,
        Func<IntPtr, IntPtr> getParent)
    {
        ArgumentNullException.ThrowIfNull(getParent);
        IntPtr current = candidateWindowHandle;
        for (int depth = 0; current != IntPtr.Zero && depth < 64; depth++)
        {
            if (current == ownerWindowHandle)
                return true;
            current = getParent(current);
        }

        return false;
    }

    private static bool IsDescendantWindow(IntPtr ownerWindowHandle, IntPtr candidateWindowHandle) =>
        IsDescendantWindow(ownerWindowHandle, candidateWindowHandle, GetParent);

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

    private static IntPtr KeyboardProcedure(int code, IntPtr wParam, IntPtr lParam)
    {
        WindowSessionHotKeyController? controller = _current;
        if (code < 0 || controller is null || controller._disposed || lParam == IntPtr.Zero)
            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

        try
        {
            KeyboardHookData data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
            bool keyDown = wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown;
            if (data.VirtualKey != VkTab)
            {
                bool ownsForeground = controller.HasForegroundOwnership();
                if (ShouldRecoverFocusBeforeKeyDispatch(
                        controller._focusRecoveryPending,
                        keyDown,
                        ownsForeground))
                {
                    controller._focusRecoveryPending = false;
                    controller.WriteDiagnostics("session-keyboard-focus-recovery");
                    controller._recoverFocus?.Invoke();
                }

                return CallNextHookEx(controller._hookHandle, code, wParam, lParam);
            }

            bool hasForegroundOwnership = controller.HasForegroundOwnership();
            controller.WriteDiagnostics(
                $"session-keyboard-tab event={wParam.ToInt64()} owner={hasForegroundOwnership} enabled={controller._enabled}");
            if (!ShouldHandleTabShortcut(controller._enabled, hasForegroundOwnership))
                return CallNextHookEx(controller._hookHandle, code, wParam, lParam);

            // Foreground ownership is authoritative. It also removes the
            // activation-event race when the user immediately presses Ctrl+Tab
            // after returning from another window.
            controller._enabled = true;
            bool keyUp = wParam == (IntPtr)WmKeyUp || wParam == (IntPtr)WmSysKeyUp;
            bool controlDown = (GetAsyncKeyState(VkControl) & 0x8000) != 0;
            if (!controller._enabled || (!controlDown && !controller._tabChordActive))
                return CallNextHookEx(controller._hookHandle, code, wParam, lParam);

            if (keyDown)
            {
                if (!controller._tabChordActive)
                {
                    controller._tabChordActive = true;
                    controller._pendingDirection = (GetAsyncKeyState(VkShift) & 0x8000) != 0 ? -1 : 1;
                    controller.WriteDiagnostics(
                        $"session-keyboard-tab-captured direction={controller._pendingDirection}");
                }

                return (IntPtr)1;
            }

            if (ShouldDispatchNavigation(keyUp, controller._tabChordActive, controller._pendingDirection))
            {
                int direction = controller._pendingDirection;
                controller._tabChordActive = false;
                controller._pendingDirection = 0;
                controller.WriteDiagnostics($"session-keyboard-tab-navigate direction={direction}");
                controller._focusRecoveryPending = true;
                DispatchNavigation(direction, controller._navigate);

                return (IntPtr)1;
            }
        }
        catch
        {
            // A hook must never block unrelated keyboard input.
        }

        return CallNextHookEx(controller._hookHandle, code, wParam, lParam);
    }

    internal static bool ShouldHandleTabShortcut(bool enabled, bool hasForegroundOwnership) =>
        enabled || hasForegroundOwnership;

    internal static bool ShouldDispatchNavigation(bool keyUp, bool tabChordActive, int pendingDirection) =>
        keyUp && tabChordActive && pendingDirection is -1 or 1;

    internal static void DispatchNavigation(
        int direction,
        Action<int> navigate)
    {
        ArgumentNullException.ThrowIfNull(navigate);

        navigate(direction);
    }

    internal static bool ShouldRecoverFocusBeforeKeyDispatch(
        bool recoveryPending,
        bool keyDown,
        bool ownsForeground) => recoveryPending && keyDown && ownsForeground;

    private void WriteDiagnostics(string message)
    {
        try
        {
            _diagnostics?.Invoke(message);
        }
        catch
        {
            // Diagnostics must never alter keyboard behaviour.
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, KeyboardHookProcedure procedure, IntPtr moduleHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr windowHandle);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr KeyboardHookProcedure(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookData
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
