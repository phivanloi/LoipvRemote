using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Browser;

/// <summary>Common protocol session adapter around the browser lifecycle and client.</summary>
public sealed class BrowserProtocolSession : IAsyncProtocolSession, IEmbeddedWindow
{
    private readonly IBrowserClient _client;
    private readonly BrowserSession _lifecycle;
    private bool _disposed;

    public BrowserProtocolSession(
        IBrowserClient client,
        BrowserConnectionOptions options,
        IEmbeddedWindowOperations? windowOperations = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _lifecycle = new BrowserSession(_client);
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _windowOperations = windowOperations;
    }

    private readonly IEmbeddedWindowOperations? _windowOperations;

    public BrowserConnectionOptions Options { get; }
    public ProtocolSessionState State => _lifecycle.State;
    public ProtocolCapabilities Capabilities => ProtocolCapabilities.EmbeddedWindow;
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
        if (_client is BrowserDesktopClient desktopClient && !desktopClient.Control.IsDisposed)
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
