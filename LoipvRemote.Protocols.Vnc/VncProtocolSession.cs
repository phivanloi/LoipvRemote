using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Vnc;

/// <summary>Common VNC session adapter around the module lifecycle.</summary>
public sealed class VncProtocolSession : IProtocolSession, IManagedEmbeddedWindow, IRemoteScreenController, IRemoteSpecialKeysController
{
    private readonly IVncClient _client;
    private readonly VncSession _lifecycle;
    private bool _disposed;

    public VncProtocolSession(
        IVncClient client,
        IVncEndpointProbe endpointProbe,
        VncConnectionOptions options,
        IEmbeddedWindowOperations? windowOperations = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        if (options.Password is not null)
            _client.SetPasswordProvider(() => options.Password);
        _lifecycle = new VncSession(_client, endpointProbe ?? throw new ArgumentNullException(nameof(endpointProbe)));
        _windowOperations = windowOperations;
    }

    private readonly IEmbeddedWindowOperations? _windowOperations;

    public VncConnectionOptions Options { get; }
    public ProtocolSessionState State => _lifecycle.State;
    public ProtocolCapabilities Capabilities => ProtocolCapabilities.EmbeddedWindow | ProtocolCapabilities.Resize;
    public bool IsAvailable => State == ProtocolSessionState.Connected;
    public IntPtr WindowHandle => _client.WindowHandle;

    private bool InitializeCore() => _lifecycle.Initialize(Options);

    public ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(InitializeCore());
    }

    public ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default) => _lifecycle.ConnectAsync(cancellationToken);

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _lifecycle.Disconnect();
        return ValueTask.CompletedTask;
    }

    public void Focus()
    {
        _client.Focus();
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
        if (_client is IManagedEmbeddedWindow managedWindow)
        {
            if (bounds.IsValid)
                managedWindow.Resize(bounds);
            return;
        }

        if (IsAvailable && bounds.IsValid && WindowHandle != IntPtr.Zero && _windowOperations is not null)
            _windowOperations.Move(WindowHandle, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    public void RefreshScreen()
    {
        _client.RefreshScreen();
    }

    public void SendSpecialKeys(RemoteSpecialKey key)
    {
        _client.SendSpecialKeys(key switch
        {
            RemoteSpecialKey.CtrlAltDel => VncSpecialKeys.CtrlAltDel,
            RemoteSpecialKey.CtrlEsc => VncSpecialKeys.CtrlEsc,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
        });
    }

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
}
