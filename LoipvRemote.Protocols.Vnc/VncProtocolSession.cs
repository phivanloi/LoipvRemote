using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Vnc;

/// <summary>Common VNC session adapter around the module lifecycle.</summary>
public sealed class VncProtocolSession : IAsyncProtocolSession, IEmbeddedWindow, IRemoteScreenController, IRemoteSpecialKeysController
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
        if (_client is VncDesktopClient desktopClient && options.Password is not null)
            desktopClient.PasswordProvider = () => options.Password;
        _lifecycle = new VncSession(_client, endpointProbe ?? throw new ArgumentNullException(nameof(endpointProbe)));
        _windowOperations = windowOperations;
    }

    private readonly IEmbeddedWindowOperations? _windowOperations;

    public VncConnectionOptions Options { get; }
    public ProtocolSessionState State => _lifecycle.State;
    public ProtocolCapabilities Capabilities => ProtocolCapabilities.EmbeddedWindow | ProtocolCapabilities.Resize;
    public bool IsAvailable => State == ProtocolSessionState.Connected;
    public IntPtr WindowHandle => _client is IEmbeddedWindow embedded ? embedded.WindowHandle : IntPtr.Zero;

    public bool Initialize() => _lifecycle.Initialize(Options);

    public ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Initialize());
    }

    public bool Connect() => _lifecycle.Connect();

    public ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Connect());
    }

    public void Disconnect() => _lifecycle.Disconnect();

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Disconnect();
        return ValueTask.CompletedTask;
    }

    public void Focus()
    {
        if (_client is VncDesktopClient desktopClient && !desktopClient.Control.IsDisposed)
            desktopClient.Control.Focus();
    }

    public bool AttachTo(IntPtr parentWindowHandle, TimeSpan timeout)
    {
        if (!IsAvailable || parentWindowHandle == IntPtr.Zero || WindowHandle == IntPtr.Zero || _windowOperations is null)
            return false;

        _windowOperations.SetParent(WindowHandle, parentWindowHandle);
        return true;
    }

    public void Resize(EmbeddedWindowBounds bounds)
    {
        if (IsAvailable && bounds.IsValid && WindowHandle != IntPtr.Zero && _windowOperations is not null)
            _windowOperations.Move(WindowHandle, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    public void RefreshScreen()
    {
        if (_client is VncDesktopClient desktopClient)
            desktopClient.RefreshScreen();
    }

    public void SendSpecialKeys(RemoteSpecialKey key)
    {
        if (_client is not VncDesktopClient desktopClient)
            return;

        desktopClient.SendSpecialKeys(key switch
        {
            RemoteSpecialKey.CtrlAltDel => VncSpecialKeys.CtrlAltDel,
            RemoteSpecialKey.CtrlEsc => VncSpecialKeys.CtrlEsc,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
        });
    }

    public void Close() => Disconnect();

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Close();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Close();
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
