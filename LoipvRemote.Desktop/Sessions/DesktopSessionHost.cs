using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;
using System.Drawing;

namespace LoipvRemote.Desktop.Sessions;

/// <summary>
/// Owns the desktop-only lifecycle around a protocol session. This class is
/// intentionally independent from the executable's WinForms controls so the
/// protocol runtime remains in its dedicated modules.
/// </summary>
public sealed class DesktopSessionHost(
    ConnectionDefinition definition,
    IProtocolSession session) : IDisposable, IInputMessageTarget
{
    private readonly ConnectionDefinition _definition =
        definition ?? throw new ArgumentNullException(nameof(definition));
    private readonly IProtocolSession _session =
        session ?? throw new ArgumentNullException(nameof(session));
    private IDesktopSessionSurface? _surface;
    private bool _embeddedWindowAttached;
    private nint _attachedWindowHandle;
    private bool _disposed;

    public ConnectionDefinition Definition => _definition;
    public IProtocolSession Session => _session;
    public ProtocolSessionState State => _session.State;
    public ProtocolCapabilities Capabilities => _session.Capabilities;
    public IDesktopSessionSurface? Surface => _surface;

    public void AttachSurface(IDesktopSessionSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (_surface is not null && !ReferenceEquals(_surface, surface))
            throw new InvalidOperationException("A protocol session host can only be attached to one desktop surface.");

        if (ReferenceEquals(_surface, surface))
            return;

        _surface = surface;
        _surface.Resize += OnResize;
    }

    public bool InitializeSurface()
    {
        if (_surface is null || _surface.IsDisposed)
            return false;

        _surface.SetParentTag(_surface);
        _surface.ShowSurface();
        return true;
    }

    public bool InitializeSession()
    {
        // In-process controls such as the RDP ActiveX host must have a WinForms
        // parent before COM activation. External process windows do not use this
        // path and continue to attach after Connect exposes their HWND.
        if (_session is IManagedEmbeddedWindow)
            AttachEmbeddedWindow();

        return _session.Initialize();
    }

    public bool Connect()
    {
        if (!_session.Connect())
            return false;

        _surface?.StartActivity();
        _embeddedWindowAttached = false;
        AttachEmbeddedWindow();
        return true;
    }

    public void Disconnect() => _session.Disconnect();

    public void Focus()
    {
        if (_surface is null || _surface.IsDisposed || !_surface.IsVisible)
            return;

        if (_session is IEmbeddedWindow embedded)
        {
            // A child process can expose its top-level window after Connect returns.
            // Retry the attachment on the first tab activation so focus is not sent
            // to an unembedded window and keyboard input is not lost.
            AttachEmbeddedWindow();
            embedded.Focus(_surface?.Handle ?? IntPtr.Zero);
            return;
        }

        _session.Focus();
    }

    public bool TryForwardInputMessage(int message, IntPtr wParam, IntPtr lParam) =>
        _session is IInputMessageTarget target && target.TryForwardInputMessage(message, wParam, lParam);

    public void Close()
    {
        if (_disposed)
            return;

        try
        {
            _session.Close();
        }
        finally
        {
            _session.Dispose();
            _surface?.StopActivity();
            _surface?.ClearParentTag();
            _surface?.DisposeSurface();
            _disposed = true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _session.Dispose();
        }
        finally
        {
            DetachSurface();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    private void AttachEmbeddedWindow()
    {
        if (_surface is null || _surface.IsDisposed ||
            _session is not IEmbeddedWindow embedded)
            return;

        nint windowHandle = embedded.WindowHandle;
        bool managedWindow = embedded is IManagedEmbeddedWindow;
        if (!managedWindow && (!embedded.IsAvailable || windowHandle == nint.Zero))
            return;

        if (_embeddedWindowAttached && _attachedWindowHandle == windowHandle)
            return;

        _embeddedWindowAttached = false;

        if (embedded.AttachTo(_surface.Handle, TimeSpan.FromSeconds(10)))
        {
            _embeddedWindowAttached = true;
            _attachedWindowHandle = windowHandle;
            Resize();
        }
    }

    private void Resize()
    {
        if (_surface is null || _surface.IsDisposed ||
            _session is not IEmbeddedWindow embedded)
            return;

        Rectangle bounds = _surface.ContentBounds;
        if (bounds.Width > 0 && bounds.Height > 0)
            embedded.Resize(new EmbeddedWindowBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }

    private void OnResize(object? sender, EventArgs e) => Resize();

    private void DetachSurface()
    {
        if (_surface is null)
            return;

        _surface.Resize -= OnResize;
        _surface = null;
        _embeddedWindowAttached = false;
        _attachedWindowHandle = nint.Zero;
    }
}
