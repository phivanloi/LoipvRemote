using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using LoipvRemote.Protocols.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
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
    private bool _disposed;

    public Win32EmbeddedSessionSurface(Window window, FrameworkElement placementTarget)
    {
        ArgumentNullException.ThrowIfNull(window);
        _placementTarget = placementTarget ?? throw new ArgumentNullException(nameof(placementTarget));
        _ownerWindowHandle = WindowNative.GetWindowHandle(window);
        _placementTarget.SizeChanged += PlacementTargetOnSizeChanged;
        _placementTarget.LayoutUpdated += PlacementTargetOnLayoutUpdated;
        UpdateBounds();
    }

    public IntPtr Handle => _nativeHost?.Handle ?? IntPtr.Zero;

    public void EnsureHostWindow()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _nativeHost ??= new WindowsChildWindowHost(_ownerWindowHandle);
        UpdateBounds();
    }

    public void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_nativeHost is not null)
            _nativeHost.SetVisible(visible);
    }

    public bool Attach(IEmbeddedWindow session, TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(session);
        EnsureHostWindow();
        IEmbeddedWindow? previousSession = _session;
        if (previousSession is not null && !ReferenceEquals(previousSession, session))
            _nativeHost?.SetChildVisible(previousSession.WindowHandle, visible: false);

        if (!session.AttachTo(Handle, timeout))
        {
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
        _nativeHost?.SetChildVisible(session.WindowHandle, visible: true);
        UpdateBounds();
        return true;
    }

    public void Focus() => _session?.Focus(Handle);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _placementTarget.SizeChanged -= PlacementTargetOnSizeChanged;
        _placementTarget.LayoutUpdated -= PlacementTargetOnLayoutUpdated;
        _nativeHost?.Dispose();
        _nativeHost = null;
        GC.SuppressFinalize(this);
    }

    private void PlacementTargetOnSizeChanged(object sender, SizeChangedEventArgs args) => UpdateBounds();

    private void PlacementTargetOnLayoutUpdated(object? sender, object args) => UpdateBounds();

    private void UpdateBounds()
    {
        if (_disposed || _nativeHost is null || _placementTarget.XamlRoot is null || _placementTarget.ActualWidth <= 0 || _placementTarget.ActualHeight <= 0)
            return;

        GeneralTransform transform = _placementTarget.TransformToVisual(null);
        Point origin = transform.TransformPoint(new Point(0, 0));
        double scale = _placementTarget.XamlRoot.RasterizationScale;
        var bounds = new EmbeddedWindowBounds(
            (int)Math.Round(origin.X * scale),
            (int)Math.Round(origin.Y * scale),
            Math.Max(1, (int)Math.Round(_placementTarget.ActualWidth * scale)),
            Math.Max(1, (int)Math.Round(_placementTarget.ActualHeight * scale)));
        _nativeHost.Resize(bounds);
        // The protocol HWND is a child of _nativeHost, whereas the native host
        // itself is a child of the WinUI top-level HWND. The protocol must be
        // sized in local coordinates; passing the WinUI offset here would push
        // RDP/PuTTY outside the tab area after every layout pass.
        _session?.Resize(EmbeddedSessionSurfaceLayout.ToProtocolBounds(bounds));
    }
}
