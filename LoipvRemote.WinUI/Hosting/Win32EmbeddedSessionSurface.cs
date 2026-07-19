using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Windows.Foundation;
using WinRT.Interop;

namespace LoipvRemote.WinUI.Hosting;

/// <summary>
/// Maps a XAML element's bounds to a dedicated child HWND. Protocol sessions use
/// that HWND as their parent, so an embedded RDP/PuTTY window fills only the tab
/// content area and never the complete top-level application window.
/// </summary>
public sealed class Win32EmbeddedSessionSurface : IEmbeddedSessionSurface, IDisposable
{
    private readonly FrameworkElement _placementTarget;
    private readonly IntPtr _ownerWindowHandle;
    private WindowsChildWindowHost? _nativeHost;
    private IEmbeddedWindow? _session;
    private EmbeddedWindowBounds? _lastReportedBounds;
    private IEmbeddedWindow? _lastResizedSession;
    private CancellationTokenSource? _focusRestoreCancellation;
    private CancellationTokenSource? _dynamicDisplayCancellation;
    private CancellationTokenSource? _windowTransitionCancellation;
    private IEmbeddedWindow? _adaptiveDisplaySession;
    private RdpDisplayConfiguration? _lastAdaptiveDisplay;
    private bool _dynamicResolutionUnavailable;
    private bool _isVisible;
    private bool _disposed;

    public Win32EmbeddedSessionSurface(Window window, FrameworkElement placementTarget)
    {
        ArgumentNullException.ThrowIfNull(window);
        _placementTarget = placementTarget ?? throw new ArgumentNullException(nameof(placementTarget));
        _ownerWindowHandle = WindowNative.GetWindowHandle(window);
        _placementTarget.SizeChanged += PlacementTargetOnSizeChanged;
        _placementTarget.LayoutUpdated += PlacementTargetOnLayoutUpdated;
        EmbeddingDiagnostics.Write($"surface-created owner={FormatHandle(_ownerWindowHandle)}");
        UpdateBoundsSafely();
    }

    public IntPtr Handle => _nativeHost?.Handle ?? IntPtr.Zero;

    public void EnsureHostWindow()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_nativeHost is null)
        {
            _nativeHost = new WindowsChildWindowHost(_ownerWindowHandle);
            EmbeddingDiagnostics.Write($"host-created {DescribeWindow(_nativeHost.Handle)}");
        }
        UpdateBoundsSafely();
    }

    public void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_nativeHost is not null)
        {
            bool visibilityChanged = _isVisible != visible;
            if (visibilityChanged)
            {
                _nativeHost.SetVisible(visible);
                _isVisible = visible;
                EmbeddingDiagnostics.Write($"host-visibility visible={visible} {DescribeWindow(_nativeHost.Handle)}");
            }
        }

        if (visible)
            UpdateBoundsSafely();
    }

    public bool Attach(IEmbeddedWindow session, TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(session);
        EnsureHostWindow();
        IEmbeddedWindow? previousSession = _session;
        if (ReferenceEquals(previousSession, session))
        {
            // RDP is attached before its ActiveX control exists. Once
            // InitializeAsync creates that control, force one local resize so
            // it does not remain at the native 1x1 creation size until a tab
            // selection causes a later layout pass.
            _lastResizedSession = null;
            UpdateBoundsSafely();
            return true;
        }

        if (previousSession is not null && !ReferenceEquals(previousSession, session))
            _nativeHost?.SetChildVisible(previousSession.WindowHandle, visible: false);

        if (!session.AttachTo(Handle, timeout))
        {
            EmbeddingDiagnostics.Write($"attach-failed session={session.GetType().Name} host={DescribeWindow(Handle)} child={DescribeWindow(session.WindowHandle)}");
            if (previousSession is not null && !ReferenceEquals(previousSession, session))
                _nativeHost?.SetChildVisible(previousSession.WindowHandle, visible: true);
            if (previousSession is null)
            {
                _nativeHost?.Dispose();
                _nativeHost = null;
            }
            return false;
        }

        _session = session;
        if (!ReferenceEquals(_adaptiveDisplaySession, session))
        {
            CancelDynamicDisplayUpdate();
            _adaptiveDisplaySession = session;
            _lastAdaptiveDisplay = null;
            _dynamicResolutionUnavailable = false;
        }
        _nativeHost?.SetChildVisible(session.WindowHandle, visible: true);
        _lastResizedSession = null;
        UpdateBoundsSafely();
        // WinUI is composed above normal child windows. Reassert the host's
        // Z-order after the protocol has attached so the remote pixels are not
        // left behind the XAML content layer.
        EmbeddingDiagnostics.Write($"attach-succeeded session={session.GetType().Name} host={DescribeWindow(Handle)} child={DescribeWindow(session.WindowHandle)}");
        return true;
    }

    public void Focus()
    {
        if (!_isVisible)
            return;

        // The owning WinUI window is already active when this method is
        // reached. Changing foreground/Z-order from here raises a second
        // activation cycle, which can continuously repaint the native SSH
        // child. Transfer keyboard focus only.
        _session?.Focus(_ownerWindowHandle);
    }

    public void RefreshLayoutAndRestoreFocus()
    {
        UpdateBoundsSafely();
        QueueFocusRestore();
        QueueWindowTransitionRefresh();
    }

    /// <summary>Stops delayed native work while Windows parks a minimized HWND off-screen.</summary>
    public void SuspendForMinimize()
    {
        if (_disposed)
            return;

        _focusRestoreCancellation?.Cancel();
        _focusRestoreCancellation?.Dispose();
        _focusRestoreCancellation = null;
        _windowTransitionCancellation?.Cancel();
        _windowTransitionCancellation?.Dispose();
        _windowTransitionCancellation = null;
        CancelDynamicDisplayUpdate();
        EmbeddingDiagnostics.Write("window-minimized; native layout suspended");
    }

    /// <summary>Reclaims terminal focus after a tab or window activation transition.</summary>
    public void RestoreFocusAfterTransition() =>
        NativeSessionFocusTransition.Restore(Focus, QueueFocusRestore);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _placementTarget.SizeChanged -= PlacementTargetOnSizeChanged;
        _placementTarget.LayoutUpdated -= PlacementTargetOnLayoutUpdated;
        _focusRestoreCancellation?.Cancel();
        _focusRestoreCancellation?.Dispose();
        CancelDynamicDisplayUpdate();
        _windowTransitionCancellation?.Cancel();
        _windowTransitionCancellation?.Dispose();
        _nativeHost?.Dispose();
        _nativeHost = null;
        EmbeddingDiagnostics.Write("surface-disposed");
        GC.SuppressFinalize(this);
    }

    private void PlacementTargetOnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        UpdateBoundsSafely();
    }

    private void PlacementTargetOnLayoutUpdated(object? sender, object args) => UpdateBoundsSafely();

    private void UpdateBoundsSafely(bool forceDynamicDisplayUpdate = false)
    {
        _ = NativeUiExceptionGuard.TryRun(
            () => UpdateBounds(forceDynamicDisplayUpdate),
            exception => EmbeddingDiagnostics.Write(
                $"bounds-update-recovered type={exception.GetType().Name} hresult=0x{exception.HResult:X8}"));
    }

    private void QueueFocusRestore()
    {
        if (_disposed || !_isVisible || _nativeHost is null || _session is null)
            return;

        _focusRestoreCancellation?.Cancel();
        _focusRestoreCancellation?.Dispose();
        CancellationTokenSource cancellation = _focusRestoreCancellation = new();
        _ = RestoreFocusAfterLayoutAsync(cancellation.Token);
    }

    private async Task RestoreFocusAfterLayoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            // A new PuTTY terminal needs one pass after its first layout to
            // accept input. Do not repeat focus requests: reactivating a
            // foreign child window repeatedly makes the terminal flicker.
            await Task.Delay(150, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
                _placementTarget.DispatcherQueue.TryEnqueue(Focus);
        }
        catch (OperationCanceledException)
        {
            // A later size/layout update superseded this focus request.
        }
    }

    private void UpdateBounds(bool forceDynamicDisplayUpdate = false)
    {
        if (_disposed || _nativeHost is null || _placementTarget.XamlRoot is null)
            return;

        if (!WindowTransitionPolicy.ShouldUpdateNativeBounds(
                IsIconic(_ownerWindowHandle),
                _placementTarget.ActualWidth,
                _placementTarget.ActualHeight))
        {
            return;
        }

        GeneralTransform transform = _placementTarget.TransformToVisual(null);
        Point origin = transform.TransformPoint(new Point(0, 0));
        double scale = _placementTarget.XamlRoot.RasterizationScale;
        var clientBounds = new EmbeddedWindowBounds(
            (int)Math.Round(origin.X * scale),
            (int)Math.Round(origin.Y * scale),
            Math.Max(1, (int)Math.Round(_placementTarget.ActualWidth * scale)),
            Math.Max(1, (int)Math.Round(_placementTarget.ActualHeight * scale)));
        NativePoint screenOrigin = new() { X = clientBounds.X, Y = clientBounds.Y };
        if (!ClientToScreen(_ownerWindowHandle, ref screenOrigin))
            throw new InvalidOperationException($"Could not position the remote session overlay (Win32 error {Marshal.GetLastWin32Error()}).");

        var hostBounds = new EmbeddedWindowBounds(
            screenOrigin.X,
            screenOrigin.Y,
            clientBounds.Width,
            clientBounds.Height);
        bool boundsChanged = _lastReportedBounds != hostBounds;
        if (boundsChanged)
        {
            _lastReportedBounds = hostBounds;
            _nativeHost.Resize(hostBounds);
            EmbeddingDiagnostics.Write($"host-resized bounds={hostBounds.X},{hostBounds.Y},{hostBounds.Width},{hostBounds.Height} {DescribeWindow(_nativeHost.Handle)}");
        }
        // The protocol HWND is a child of _nativeHost, whereas the native host
        // itself is a child of the WinUI top-level HWND. The protocol must be
        // sized in local coordinates; passing the WinUI offset here would push
        // RDP/PuTTY outside the tab area after every layout pass.
        if (_session is not null && (boundsChanged || !ReferenceEquals(_lastResizedSession, _session)))
        {
            _session.Resize(EmbeddedSessionSurfaceLayout.ToProtocolBounds(hostBounds));
            _lastResizedSession = _session;
        }

        UpdateAdaptiveRdpDisplay(hostBounds, scale, forceDynamicDisplayUpdate);
    }

    private void UpdateAdaptiveRdpDisplay(
        EmbeddedWindowBounds hostBounds,
        double rasterizationScale,
        bool forceDynamicDisplayUpdate = false)
    {
        if (_session is not IAdaptiveRdpDisplaySession adaptive ||
            _session is not IProtocolSession protocolSession)
        {
            return;
        }

        RdpDisplayConfiguration display = RdpDisplaySizing.CreateAuto(
            hostBounds.Width,
            hostBounds.Height,
            rasterizationScale);

        if (protocolSession.State == ProtocolSessionState.Initialized)
        {
            if (!Equals(_lastAdaptiveDisplay, display))
            {
                adaptive.PrepareDisplay(display);
                _lastAdaptiveDisplay = display;
                EmbeddingDiagnostics.Write($"rdp-display-prepared size={display.Width}x{display.Height} scale={display.DesktopScaleFactor}");
            }
            return;
        }

        if (protocolSession.State != ProtocolSessionState.Connected ||
            _dynamicResolutionUnavailable ||
            (!forceDynamicDisplayUpdate && !NeedsDynamicDisplayUpdate(display)))
        {
            return;
        }

        QueueDynamicDisplayUpdate(adaptive, display, forceDynamicDisplayUpdate);
    }

    private bool NeedsDynamicDisplayUpdate(RdpDisplayConfiguration display) =>
        !Equals(_lastAdaptiveDisplay, display);

    private void QueueDynamicDisplayUpdate(
        IAdaptiveRdpDisplaySession adaptive,
        RdpDisplayConfiguration display,
        bool force)
    {
        CancelDynamicDisplayUpdate();
        CancellationTokenSource cancellation = _dynamicDisplayCancellation = new();
        _ = ApplyDynamicDisplayAfterResizeAsync(adaptive, display, force, cancellation.Token);
    }

    private async Task ApplyDynamicDisplayAfterResizeAsync(
        IAdaptiveRdpDisplaySession adaptive,
        RdpDisplayConfiguration display,
        bool force,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested || _disposed)
                return;

            _placementTarget.DispatcherQueue.TryEnqueue(() =>
            {
                if (cancellationToken.IsCancellationRequested ||
                    _disposed ||
                    !ReferenceEquals(_session, _adaptiveDisplaySession) ||
                    (!force && !NeedsDynamicDisplayUpdate(display)))
                {
                    return;
                }

                if (adaptive.TryUpdateDisplay(display))
                {
                    _lastAdaptiveDisplay = display;
                    EmbeddingDiagnostics.Write($"rdp-display-updated size={display.Width}x{display.Height} scale={display.DesktopScaleFactor}");
                }
                else
                {
                    _dynamicResolutionUnavailable = true;
                    EmbeddingDiagnostics.Write("rdp-display-dynamic-update-unavailable; keeping SmartSizing fallback");
                }
            });
        }
        catch (OperationCanceledException)
        {
            // The user is still resizing or a different session became active.
        }
    }

    private void CancelDynamicDisplayUpdate()
    {
        _dynamicDisplayCancellation?.Cancel();
        _dynamicDisplayCancellation?.Dispose();
        _dynamicDisplayCancellation = null;
    }

    private void QueueWindowTransitionRefresh()
    {
        _windowTransitionCancellation?.Cancel();
        _windowTransitionCancellation?.Dispose();
        CancellationTokenSource cancellation = _windowTransitionCancellation = new();
        _ = RefreshAfterWindowTransitionAsync(cancellation.Token);
    }

    private async Task RefreshAfterWindowTransitionAsync(CancellationToken cancellationToken)
    {
        try
        {
            // AppWindow.Changed is raised before the final XAML measure pass
            // for maximize/restore. Sample the settled client region once more
            // so a remote desktop never keeps the previous, taller viewport
            // and clips its taskbar at the bottom.
            await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken).ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested && !_disposed)
                _placementTarget.DispatcherQueue.TryEnqueue(() => UpdateBoundsSafely(forceDynamicDisplayUpdate: true));
        }
        catch (OperationCanceledException)
        {
            // A later window transition superseded this refresh.
        }
    }

    private static string DescribeWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return "hwnd=0";

        _ = GetWindowRect(handle, out NativeRect rect);
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"hwnd={FormatHandle(handle)} parent={FormatHandle(GetParent(handle))} visible={IsWindowVisible(handle)} rect={rect.Left},{rect.Top},{rect.Right - rect.Left},{rect.Bottom - rect.Top}");
    }

    private static string FormatHandle(IntPtr handle) =>
        $"0x{handle.ToInt64():X}";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetParent(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClientToScreen(IntPtr windowHandle, ref NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
