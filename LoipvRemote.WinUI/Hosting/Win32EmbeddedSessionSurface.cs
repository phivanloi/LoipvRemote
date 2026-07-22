using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private static readonly TimeSpan[] FocusRetryDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(75),
        TimeSpan.FromMilliseconds(175),
        TimeSpan.FromMilliseconds(350)
    ];
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
    private FrameworkElement[] _xamlOverlayElements = [];
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

    /// <summary>
    /// Returns the actual cross-process terminal HWND only when the active
    /// protocol supports verified focus recovery. The shared native host is
    /// deliberately excluded so shell clicks can never trigger SSH focus.
    /// </summary>
    public IntPtr FocusTargetWindowHandle =>
        _session is IEmbeddedWindowFocusTarget ? _session.WindowHandle : IntPtr.Zero;

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
        if (!visible)
            CancelPendingFocusRestore();
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
        IEmbeddedWindow? targetSession = _session;
        if (_placementTarget.DispatcherQueue.HasThreadAccess)
        {
            _ = TryFocusSession(targetSession);
            return;
        }

        // Native keyboard hooks run on their own thread. Queueing here keeps
        // GetFocus/SetFocus on the WinUI input queue that owns the host HWND.
        _ = _placementTarget.DispatcherQueue.TryEnqueue(() => _ = TryFocusSession(targetSession));
    }

    public void RefreshLayoutAndRestoreFocus()
    {
        UpdateBoundsSafely();
        QueueFocusRestore();
        QueueWindowTransitionRefresh();
    }

    /// <summary>
    /// Completes a maximize/restore layout pass without taking focus away
    /// from the shell control whose native pointer action is still running.
    /// </summary>
    public void RefreshLayoutAfterWindowTransition()
    {
        UpdateBoundsSafely();
        QueueWindowTransitionRefresh();
    }

    /// <summary>Stops delayed native work while Windows parks a minimized HWND off-screen.</summary>
    public void SuspendForMinimize()
    {
        if (_disposed)
            return;

        CancelPendingFocusRestore();
        _windowTransitionCancellation?.Cancel();
        _windowTransitionCancellation?.Dispose();
        _windowTransitionCancellation = null;
        CancelDynamicDisplayUpdate();
        EmbeddingDiagnostics.Write("window-minimized; native layout suspended");
    }

    /// <summary>Reclaims terminal focus after a tab or window activation transition.</summary>
    public void RestoreFocusAfterTransition() =>
        NativeSessionFocusTransition.Restore(Focus, QueueFocusRestore);

    /// <summary>
    /// Cancels delayed focus attempts when the user intentionally clicks a
    /// shell control outside the embedded PuTTY window. This method is safe to
    /// call from the low-level mouse-hook thread.
    /// </summary>
    public void CancelPendingFocusRestore()
    {
        CancellationTokenSource? cancellation = Interlocked.Exchange(
            ref _focusRestoreCancellation,
            null);
        CancelAndDispose(cancellation);
    }

    /// <summary>
    /// Keeps the native session visible while exposing only the rectangles
    /// needed by WinUI menus and dialogs above the owned native popup.
    /// </summary>
    public void SetXamlOverlayOcclusions(IEnumerable<FrameworkElement> overlays)
    {
        ArgumentNullException.ThrowIfNull(overlays);
        _xamlOverlayElements = overlays
            .Where(element => element is not null)
            .Distinct()
            .ToArray();
        ApplyXamlOverlayOcclusions();
    }

    public void ClearXamlOverlayOcclusions()
    {
        _xamlOverlayElements = [];
        _nativeHost?.ClearOccludedRegions();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _placementTarget.SizeChanged -= PlacementTargetOnSizeChanged;
        _placementTarget.LayoutUpdated -= PlacementTargetOnLayoutUpdated;
        _xamlOverlayElements = [];
        CancelPendingFocusRestore();
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

        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous = Interlocked.Exchange(
            ref _focusRestoreCancellation,
            cancellation);
        CancelAndDispose(previous);
        IEmbeddedWindow targetSession = _session;
        _ = RestoreFocusAfterLayoutAsync(targetSession, cancellation.Token);
    }

    private async Task RestoreFocusAfterLayoutAsync(
        IEmbeddedWindow targetSession,
        CancellationToken cancellationToken)
    {
        try
        {
            // A new PuTTY terminal needs one pass after its first layout to
            // accept input. A first-use host-key alert may remain open for an
            // arbitrary amount of time, so wait for that native modal dialog
            // to close before bounded, verified focus attempts are dispatched.
            await Task.Delay(150, cancellationToken);
            await NativeSessionFocusTransition.WaitUntilUnblockedAsync(
                () => ReferenceEquals(_session, targetSession) &&
                    targetSession is IEmbeddedWindowFocusDeferral { IsFocusBlocked: true },
                TimeSpan.FromMilliseconds(100),
                cancellationToken);
            _ = await NativeSessionFocusTransition.RestoreUntilSuccessfulAsync(
                token => DispatchFocusAttemptAsync(targetSession, token),
                () => !_disposed && _isVisible && ReferenceEquals(_session, targetSession),
                FocusRetryDelays,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A later size/layout update superseded this focus request.
        }
    }

    private static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
            return;

        try
        {
            cancellation.Cancel();
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private bool TryFocusSession(IEmbeddedWindow? targetSession)
    {
        if (_disposed || !_isVisible || targetSession is null ||
            !ReferenceEquals(_session, targetSession))
        {
            return false;
        }

        // The owning WinUI window is already active here. PuTTY exposes a
        // verified transfer so a rejected SetFocus never becomes a silent
        // success; other embedded protocols retain their existing contract.
        bool focused;
        try
        {
            if (targetSession is IEmbeddedWindowFocusTarget focusTarget)
            {
                focused = focusTarget.TryFocus(_ownerWindowHandle);
            }
            else
            {
                targetSession.Focus(_ownerWindowHandle);
                focused = true;
            }
        }
        catch (Exception exception)
        {
            EmbeddingDiagnostics.Write(
                $"session-focus-recovered type={exception.GetType().Name} hresult=0x{exception.HResult:X8}");
            return false;
        }

        EmbeddingDiagnostics.Write(
            $"session-focus-attempt session={targetSession.GetType().Name} focused={focused} child={FormatHandle(targetSession.WindowHandle)}");
        return focused;
    }

    private async ValueTask<bool> DispatchFocusAttemptAsync(
        IEmbeddedWindow targetSession,
        CancellationToken cancellationToken)
    {
        if (_placementTarget.DispatcherQueue.HasThreadAccess)
            return TryFocusSession(targetSession);

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_placementTarget.DispatcherQueue.TryEnqueue(
                () => completion.TrySetResult(TryFocusSession(targetSession))))
        {
            return false;
        }

        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
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
        ApplyXamlOverlayOcclusions(hostBounds);
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

    private void ApplyXamlOverlayOcclusions() 
    {
        if (_lastReportedBounds is EmbeddedWindowBounds hostBounds)
            ApplyXamlOverlayOcclusions(hostBounds);
    }

    private void ApplyXamlOverlayOcclusions(EmbeddedWindowBounds hostBounds)
    {
        if (_nativeHost is null)
            return;

        EmbeddedWindowBounds[] holes = _xamlOverlayElements
            .Select(element => new
            {
                Element = element,
                Bounds = TryGetOverlayScreenBounds(element)
            })
            .Where(item => item.Bounds.HasValue)
            .Select(item => OverlayOcclusionPolicy.ToHostLocalHole(
                hostBounds,
                item.Bounds!.Value,
                padding: item.Element is MenuFlyoutPresenter ? 6 : 96))
            .Where(bounds => bounds.HasValue)
            .Select(bounds => bounds!.Value)
            .ToArray();
        if (_xamlOverlayElements.Length > 0)
        {
            EmbeddingDiagnostics.Write(
                $"xaml-overlay-occlusion overlays={_xamlOverlayElements.Length} holes={holes.Length} " +
                $"host={hostBounds.X},{hostBounds.Y},{hostBounds.Width},{hostBounds.Height} " +
                $"regions={string.Join(';', holes.Select(hole => $"{hole.X},{hole.Y},{hole.Width},{hole.Height}"))}");
        }
        _nativeHost.SetOccludedRegions(holes);
        if (holes.Length > 0)
        {
            // Opening a WinUI popup moves its composition surface above the
            // owned native host. Raise the clipped host without activating it;
            // the dialog/menu remains visible through the excluded rectangle.
            _nativeHost.BringToFront();
        }
    }

    private EmbeddedWindowBounds? TryGetOverlayScreenBounds(FrameworkElement overlay)
    {
        if (overlay.XamlRoot is null ||
            overlay.XamlRoot.RasterizationScale <= 0 ||
            overlay.ActualWidth <= 0 ||
            overlay.ActualHeight <= 0)
        {
            return null;
        }

        try
        {
            Point origin = overlay.TransformToVisual(null)
                .TransformPoint(new Point(0, 0));
            double scale = overlay.XamlRoot.RasterizationScale;
            NativePoint screenOrigin = new()
            {
                X = (int)Math.Round(origin.X * scale),
                Y = (int)Math.Round(origin.Y * scale)
            };
            if (!ClientToScreen(_ownerWindowHandle, ref screenOrigin))
                return null;

            return new EmbeddedWindowBounds(
                screenOrigin.X,
                screenOrigin.Y,
                Math.Max(1, (int)Math.Round(overlay.ActualWidth * scale)),
                Math.Max(1, (int)Math.Round(overlay.ActualHeight * scale)));
        }
        catch (InvalidOperationException)
        {
            return null;
        }
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
