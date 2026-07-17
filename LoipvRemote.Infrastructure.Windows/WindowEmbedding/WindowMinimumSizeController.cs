using System.Runtime.InteropServices;

namespace LoipvRemote.Infrastructure.Windows.WindowEmbedding;

/// <summary>
/// Applies a native minimum tracking size to a top-level WinUI window. XAML
/// column widths alone cannot prevent Windows from resizing the HWND below the
/// point where a fixed navigation pane becomes unusable.
/// </summary>
public sealed class WindowMinimumSizeController : IDisposable
{
    private const uint WmGetMinMaxInfo = 0x0024;
    private static readonly SubclassProcedure Callback = WindowProcedure;
    private static long _nextSubclassId;
    private readonly IntPtr _windowHandle;
    private readonly UIntPtr _subclassId;
    private GCHandle _selfHandle;
    private bool _disposed;

    public WindowMinimumSizeController(IntPtr windowHandle, int minimumWidth, int minimumHeight)
    {
        if (windowHandle == IntPtr.Zero)
            throw new ArgumentException("A top-level window handle is required.", nameof(windowHandle));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumHeight);

        _windowHandle = windowHandle;
        MinimumWidth = minimumWidth;
        MinimumHeight = minimumHeight;
        _subclassId = new UIntPtr((ulong)Interlocked.Increment(ref _nextSubclassId));
        _selfHandle = GCHandle.Alloc(this);
        if (!SetWindowSubclass(_windowHandle, Callback, _subclassId, GCHandle.ToIntPtr(_selfHandle)))
        {
            _selfHandle.Free();
            throw new InvalidOperationException($"Could not set the WinUI minimum window size (Win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    public int MinimumWidth { get; }

    public int MinimumHeight { get; }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _ = RemoveWindowSubclass(_windowHandle, Callback, _subclassId);
        if (_selfHandle.IsAllocated)
            _selfHandle.Free();
        GC.SuppressFinalize(this);
    }

    private static IntPtr WindowProcedure(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        IntPtr referenceData)
    {
        if (message == WmGetMinMaxInfo && lParam != IntPtr.Zero && referenceData != IntPtr.Zero &&
            GCHandle.FromIntPtr(referenceData).Target is WindowMinimumSizeController controller && !controller._disposed)
        {
            MinMaxInfo info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            info.MinimumTrackingSize.X = Math.Max(info.MinimumTrackingSize.X, controller.MinimumWidth);
            info.MinimumTrackingSize.Y = Math.Max(info.MinimumTrackingSize.Y, controller.MinimumHeight);
            Marshal.StructureToPtr(info, lParam, fDeleteOld: false);
        }

        return DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr windowHandle, SubclassProcedure procedure, UIntPtr subclassId, IntPtr referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(IntPtr windowHandle, SubclassProcedure procedure, UIntPtr subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SubclassProcedure(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, IntPtr referenceData);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaximumSize;
        public Point MaximumPosition;
        public Point MinimumTrackingSize;
        public Point MaximumTrackingSize;
    }
}
