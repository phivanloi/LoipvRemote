using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Rdp;

/// <summary>Common RDP session adapter around the module lifecycle.</summary>
public sealed class RdpProtocolSession : IProtocolSession, IProtocolSessionEvents, IManagedEmbeddedWindow, IViewOnlySession, ISmartSizingSession, IFullscreenSession
{
    private readonly IRdpClient _client;
    private readonly RdpSession _lifecycle;
    private bool _disposed;

    public RdpProtocolSession(
        IRdpClient client,
        RdpConnectionOptions options,
        IEmbeddedWindowOperations? windowOperations = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _lifecycle = new RdpSession(_client);
        _windowOperations = windowOperations;
    }

    private readonly IEmbeddedWindowOperations? _windowOperations;

    public RdpConnectionOptions Options { get; }
    public ProtocolSessionState State => _lifecycle.State;
    public ProtocolCapabilities Capabilities => ProtocolCapabilities.EmbeddedWindow | ProtocolCapabilities.Resize;
    public bool IsAvailable => _client is IEmbeddedWindow embedded && embedded.IsAvailable;
    public IntPtr WindowHandle => _client is IEmbeddedWindow embedded ? embedded.WindowHandle : IntPtr.Zero;

    public event EventHandler? Connecting;
    public event EventHandler? Connected;
    public event EventHandler<ProtocolSessionDisconnectedEventArgs>? Disconnected;
    public event EventHandler<ProtocolSessionErrorEventArgs>? ErrorOccurred;

    private bool InitializeCore()
    {
        bool initialized = _lifecycle.Initialize(Options);
        if (initialized)
            SubscribeRuntimeEvents();
        return initialized;
    }

    public ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(InitializeCore());
    }

    private bool ConnectCore() => _lifecycle.Connect();

    public ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ConnectCore());
    }

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _lifecycle.Disconnect();
        return ValueTask.CompletedTask;
    }

    public void Focus()
    {
        if (_client is IEmbeddedWindow embeddedWindow)
            embeddedWindow.Focus();
    }

    public bool AttachTo(IntPtr parentWindowHandle, TimeSpan timeout)
    {
        if (parentWindowHandle == IntPtr.Zero)
            return false;

        if (_client is IManagedEmbeddedWindow managedWindow)
            return managedWindow.AttachTo(parentWindowHandle, timeout);

        if (!IsAvailable || WindowHandle == IntPtr.Zero || _windowOperations is null)
            return false;

        _windowOperations.SetParent(WindowHandle, parentWindowHandle);
        return true;
    }

    public void Resize(EmbeddedWindowBounds bounds)
    {
        if (IsAvailable && bounds.IsValid && WindowHandle != IntPtr.Zero && _windowOperations is not null)
            _windowOperations.Move(WindowHandle, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    public bool ViewOnly
    {
        get => (_client as IRdpDisplayClient)?.ViewOnly ?? false;
        set
        {
            if (_client is IRdpDisplayClient display)
                display.ViewOnly = value;
        }
    }

    public void ToggleViewOnly() => ViewOnly = !ViewOnly;

    public bool SmartSize
    {
        get => (_client as IRdpDisplayClient)?.SmartSize ?? false;
        set
        {
            if (_client is IRdpDisplayClient display)
                display.SmartSize = value;
        }
    }

    public void ToggleSmartSize() => SmartSize = !SmartSize;

    public bool Fullscreen
    {
        get => (_client as IRdpDisplayClient)?.FullScreen ?? false;
        set
        {
            if (_client is IRdpDisplayClient display)
                display.FullScreen = value;
        }
    }

    public void ToggleFullscreen() => Fullscreen = !Fullscreen;

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _lifecycle.Disconnect();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        UnsubscribeRuntimeEvents();
        _lifecycle.Disconnect();
        if (_client is IDisposable disposable)
            disposable.Dispose();
        _disposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private void SubscribeRuntimeEvents()
    {
        if (_client is not IRdpEventClient events)
            return;

        events.Connecting += OnConnecting;
        events.Connected += OnConnected;
        events.FatalError += OnFatalError;
        events.Disconnected += OnDisconnected;
        events.SubscribeEvents();
    }

    private void UnsubscribeRuntimeEvents()
    {
        if (_client is not IRdpEventClient events)
            return;

        events.Connecting -= OnConnecting;
        events.Connected -= OnConnected;
        events.FatalError -= OnFatalError;
        events.Disconnected -= OnDisconnected;
        events.UnsubscribeEvents();
    }

    private void OnConnecting(object? sender, EventArgs args) => Connecting?.Invoke(this, EventArgs.Empty);

    private void OnConnected(object? sender, EventArgs args)
    {
        _lifecycle.MarkConnected();
        Connected?.Invoke(this, EventArgs.Empty);
    }

    private void OnFatalError(object? sender, int code)
    {
        _lifecycle.MarkFaulted();
        ErrorOccurred?.Invoke(this, new ProtocolSessionErrorEventArgs($"RDP fatal error {code}.", code));
    }

    private void OnDisconnected(object? sender, int reason)
    {
        _lifecycle.MarkClosed();
        string message = (_client as IRdpEventClient)?.GetErrorDescription(reason)
            ?? $"RDP disconnected ({reason}).";
        Disconnected?.Invoke(this, new ProtocolSessionDisconnectedEventArgs(message, reason));
    }
}
