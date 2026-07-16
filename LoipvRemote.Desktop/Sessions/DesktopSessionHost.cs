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
    IProtocolSession session) : IDisposable, IAsyncDisposable, IInputMessageTarget
{
    private readonly ConnectionDefinition _definition =
        definition ?? throw new ArgumentNullException(nameof(definition));
    private readonly IProtocolSession _session =
        session ?? throw new ArgumentNullException(nameof(session));
    private IDesktopSessionSurface? _surface;
    private SynchronizationContext? _surfaceContext;
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
        _surfaceContext = SynchronizationContext.Current;
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

    public async ValueTask<bool> InitializeSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await RunOnSurfaceThreadAsync(async () =>
        {
            if (_surface is not null && _session is IEmbeddedWindowHost host)
                host.SetHostWindowHandle(_surface.Handle);

            if (_session is IManagedEmbeddedWindow)
                AttachEmbeddedWindow();

            return await _session.InitializeAsync(cancellationToken).ConfigureAwait(true);
        }).ConfigureAwait(false);
    }

    public async ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (!await RunOnSurfaceThreadAsync(
                () => _session.ConnectAsync(cancellationToken).AsTask()).ConfigureAwait(false))
            return false;

        RunOnSurfaceThread(() =>
        {
            _surface?.StartActivity();
            _embeddedWindowAttached = false;
            AttachEmbeddedWindow();
        });
        return true;
    }

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await RunOnSurfaceThreadAsync(async () =>
        {
            await _session.DisconnectAsync(cancellationToken).ConfigureAwait(true);
            return true;
        }).ConfigureAwait(false);
    }

    public void Focus() => RunOnSurfaceThread(FocusOnSurfaceThread);

    private void FocusOnSurfaceThread()
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

    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        try
        {
            await RunOnSurfaceThreadAsync(async () =>
            {
                await _session.CloseAsync(cancellationToken).ConfigureAwait(true);
                return true;
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnSurfaceThreadAsync(async () =>
            {
                await _session.DisposeAsync().ConfigureAwait(true);
                return true;
            }).ConfigureAwait(false);
            RunOnSurfaceThread(() =>
            {
                _surface?.StopActivity();
                _surface?.ClearParentTag();
                _surface?.DisposeSurface();
            });
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            await RunOnSurfaceThreadAsync(async () =>
            {
                await _session.DisposeAsync().ConfigureAwait(true);
                return true;
            }).ConfigureAwait(false);
        }
        finally
        {
            RunOnSurfaceThread(DetachSurface);
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            RunOnSurfaceThread(_session.Dispose);
        }
        finally
        {
            RunOnSurfaceThread(DetachSurface);
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
        {
            // The first attach can occur before WinForms has completed the
            // document layout. Reapply the current client bounds whenever the
            // active tab requests focus so external windows cannot remain at
            // their startup size or position.
            Resize();
            return;
        }

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
        _surfaceContext = null;
        _embeddedWindowAttached = false;
        _attachedWindowHandle = nint.Zero;
    }

    private void RunOnSurfaceThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        SynchronizationContext? context = _surfaceContext;
        if (context is null || ReferenceEquals(SynchronizationContext.Current, context))
        {
            action();
            return;
        }

        context.Send(static state => ((Action)state!).Invoke(), action);
    }

    private Task<T> RunOnSurfaceThreadAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        SynchronizationContext? context = _surfaceContext;
        if (context is null || ReferenceEquals(SynchronizationContext.Current, context))
            return action();

        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(static state =>
        {
            var work = ((Func<Task<T>> Action, TaskCompletionSource<T> Completion))state!;
            _ = CompleteOnSurfaceThreadAsync(work.Action, work.Completion);
        }, (action, completion));
        return completion.Task;
    }

    private static async Task CompleteOnSurfaceThreadAsync<T>(
        Func<Task<T>> action,
        TaskCompletionSource<T> completion)
    {
        try
        {
            completion.TrySetResult(await action().ConfigureAwait(true));
        }
        catch (OperationCanceledException exception)
        {
            completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }
}
