using System.Runtime.InteropServices;
using System.Text;
using LoipvRemote.Infrastructure.Windows.Interop;

namespace LoipvRemote.Infrastructure.Windows.WindowEmbedding;

/// <summary>Theo dõi việc đóng thông báo lỗi mạng của đúng tiến trình PuTTY.</summary>
public sealed class WindowsPuttyNetworkErrorMonitor : IDisposable
{
    private readonly Action _acknowledged;
    private readonly WinEventCallback _callback;
    private readonly HashSet<nint> _networkDialogs = [];
    private nint _hook;

    private WindowsPuttyNetworkErrorMonitor(Action acknowledged)
    {
        _acknowledged = acknowledged;
        _callback = OnWinEvent;
    }

    /// <summary>Đăng ký theo dõi; trả null nếu Windows không cho phép tạo hook.</summary>
    public static WindowsPuttyNetworkErrorMonitor? TryCreate(int processId, Action acknowledged)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        ArgumentNullException.ThrowIfNull(acknowledged);
        var monitor = new WindowsPuttyNetworkErrorMonitor(acknowledged);
        // Out-of-context callbacks run on the registering UI thread. Track
        // show/hide/destroy so even a quick OK between timer ticks is observed.
        monitor._hook = SetWinEventHook(0x8001, 0x8003, 0, monitor._callback, (uint)processId, 0, 0);
        return monitor._hook == 0 ? null : monitor;
    }

    private void OnWinEvent(nint hook, uint eventType, nint window, int objectId, int childId, uint threadId, uint time)
    {
        if (_hook == 0 || window == 0 || objectId != 0 || childId != 0)
            return;
        try
        {
            if (eventType == 0x8002)
            {
                var className = new StringBuilder(64);
                _ = NativeMethods.GetClassName(window, className, className.Capacity);
                if (!className.ToString().Equals("#32770", StringComparison.Ordinal))
                    return;
                var title = new StringBuilder(128);
                _ = NativeMethods.GetWindowText(window, title, title.Capacity);
                if (!title.ToString().Equals("PuTTY Fatal Error", StringComparison.OrdinalIgnoreCase))
                    return;

                nint child = 0;
                for (var index = 0; index < 32; index++)
                {
                    child = NativeMethods.FindWindowEx(window, child, null, null);
                    if (child == 0)
                        break;
                    className.Clear();
                    _ = NativeMethods.GetClassName(child, className, className.Capacity);
                    if (!className.ToString().Equals("Static", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var text = new char[512];
                    if (SendMessageTimeout(child, 0x000D, (nuint)text.Length, text, 0x0002, 100, out var copied) == 0)
                        continue;
                    if (IsTransientNetworkError(new string(text, 0, (int)Math.Min(copied, (nuint)text.Length))))
                    {
                        _networkDialogs.Add(window);
                        break;
                    }
                }
            }
            else if (eventType == 0x8001 && _networkDialogs.Remove(window))
            {
                // Do not dismiss the dialog ourselves or bypass trust/auth
                // prompts. Reconnect only after the network error is closed.
                _acknowledged();
            }
        }
        catch (Exception)
        {
            // Exceptions must never cross an unmanaged Windows event callback.
        }
    }

    internal static bool IsTransientNetworkError(string text) => text.Trim() is
        "Network error: Software caused connection abort" or
        "Network error: Connection reset by peer" or
        "Network error: Connection timed out" or
        "Remote side unexpectedly closed network connection";

    public void Dispose()
    {
        var hook = Interlocked.Exchange(ref _hook, 0);
        if (hook != 0)
            _ = UnhookWinEvent(hook);
        GC.KeepAlive(_callback);
        GC.SuppressFinalize(this);
    }

    private delegate void WinEventCallback(nint hook, uint eventType, nint window, int objectId, int childId, uint threadId, uint time);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventCallback callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint hook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessageTimeout(nint window, uint message, nuint wParam, [Out] char[] text, uint flags, uint timeout, out nuint result);
}
