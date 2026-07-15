using LoipvRemote.Domain.Protocols;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.ExternalApps;

/// <summary>Lifecycle implementation for a configured external application.</summary>
public sealed class ExternalApplicationSession : IAsyncProtocolSession, IEmbeddedWindow
{
    private readonly ExternalApplicationDefinition _definition;
    private readonly IExternalApplicationHost _host;
    private bool _disposed;

    public ExternalApplicationSession(ExternalApplicationDefinition definition, IExternalApplicationHost host)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _host.Exited += HostOnExited;
    }

    public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;
    public IntPtr WindowHandle => _host.WindowHandle;
    public string WindowTitle => _host.WindowTitle;

    public event EventHandler? Exited;

    public ProtocolCapabilities Capabilities => _definition.EmbedWindow
        ? ProtocolCapabilities.EmbeddedWindow | ProtocolCapabilities.Resize
        : ProtocolCapabilities.None;

    public bool IsAvailable => State == ProtocolSessionState.Connected && _host.IsRunning;

    public bool Initialize()
    {
        if (State != ProtocolSessionState.Created || !_definition.IsValid)
        {
            State = ProtocolSessionState.Faulted;
            return false;
        }

        State = ProtocolSessionState.Initialized;
        return true;
    }

    public ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Initialize());
    }

    public bool Connect()
    {
        if (State != ProtocolSessionState.Initialized || !_host.Start(_definition))
        {
            State = ProtocolSessionState.Faulted;
            return false;
        }

        State = ProtocolSessionState.Connected;
        return true;
    }

    public ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Connect());
    }

    public void Disconnect() => Close();

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Disconnect();
        return ValueTask.CompletedTask;
    }

    public void Focus()
    {
        if (IsAvailable)
            _host.Focus();
    }

    public bool AttachTo(IntPtr parentWindowHandle, TimeSpan timeout)
    {
        return IsAvailable &&
               _definition.EmbedWindow &&
               _host.WaitForMainWindow(timeout) &&
               _host.AttachTo(parentWindowHandle);
    }

    public void Resize(EmbeddedWindowBounds bounds)
    {
        if (IsAvailable && _definition.EmbedWindow && bounds.IsValid)
            _host.Resize(bounds);
    }

    public void Close()
    {
        if (State == ProtocolSessionState.Closed)
            return;

        State = ProtocolSessionState.Closing;
        if (_host.IsRunning)
            _host.Close();

        State = ProtocolSessionState.Closed;
    }

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
        _host.Exited -= HostOnExited;
        _host.Dispose();
        _disposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private void HostOnExited(object? sender, EventArgs e)
    {
        if (State is ProtocolSessionState.Closed or ProtocolSessionState.Closing)
            return;

        State = ProtocolSessionState.Closed;
        Exited?.Invoke(this, EventArgs.Empty);
    }
}
