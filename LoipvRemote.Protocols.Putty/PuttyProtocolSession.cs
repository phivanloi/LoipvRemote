using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Putty;

/// <summary>PuTTY lifecycle, embedding and keyboard routing owned by the protocol module.</summary>
public sealed class PuttyProtocolSession : IAsyncProtocolSession, IEmbeddedWindow, IInputMessageTarget, IPuttySettingsSession
{
    private readonly IPuttyProcessHost _process;
    private readonly IEmbeddedWindowOperations _windowOperations;
    private readonly PuttyConnectionOptions _options;
    private bool _disposed;

    public PuttyProtocolSession(
        IPuttyProcessHost process,
        IEmbeddedWindowOperations windowOperations,
        PuttyConnectionOptions options)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _windowOperations = windowOperations ?? throw new ArgumentNullException(nameof(windowOperations));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;
    public ProtocolCapabilities Capabilities =>
        ProtocolCapabilities.EmbeddedWindow |
        ProtocolCapabilities.Resize |
        ProtocolCapabilities.Reconnect |
        ProtocolCapabilities.InputForwarding;
    public bool IsAvailable => State == ProtocolSessionState.Connected && _process.IsRunning;
    public nint WindowHandle => _process.MainWindowHandle;
    public string WindowTitle => _process.MainWindowTitle;

    public bool Initialize()
    {
        if (State is not ProtocolSessionState.Created and not ProtocolSessionState.Closed)
            return false;

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
        if (State != ProtocolSessionState.Initialized)
            return false;

        bool started = _process.Start(
            new PuttyProcessStartOptions(_options.ExecutablePath, PuttyLaunchArguments.Build(_options.LaunchOptions), _options.StartMinimized),
            ProcessExited);
        State = started ? ProtocolSessionState.Connected : ProtocolSessionState.Faulted;
        return started;
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
        => Focus(IntPtr.Zero);

    public void Focus(IntPtr ownerWindowHandle)
    {
        if (!IsAvailable)
            return;

        _windowOperations.Activate(WindowHandle);
        if (ownerWindowHandle != IntPtr.Zero &&
            _windowOperations.TryFocus(ownerWindowHandle, WindowHandle))
            return;

        _windowOperations.SetFocus(WindowHandle);
    }

    public bool AttachTo(IntPtr parentWindowHandle, TimeSpan timeout)
    {
        if (!IsAvailable || parentWindowHandle == IntPtr.Zero || WindowHandle == IntPtr.Zero)
            return false;

        _windowOperations.SetParent(WindowHandle, parentWindowHandle);
        return true;
    }

    public void Resize(EmbeddedWindowBounds bounds)
    {
        if (IsAvailable && bounds.IsValid)
            _windowOperations.Move(WindowHandle, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    public bool TryForwardInputMessage(int message, IntPtr wParam, IntPtr lParam)
    {
        if (!IsAvailable || !PuttyImeMessageRouter.ShouldForward(message))
            return false;

        _windowOperations.SendMessage(WindowHandle, (uint)message, wParam, lParam);
        return true;
    }

    public void ShowSettingsDialog()
    {
        if (IsAvailable)
            _windowOperations.ShowSettingsDialog(WindowHandle, 0x50);
    }

    public void Close()
    {
        if (State == ProtocolSessionState.Closed)
            return;

        State = ProtocolSessionState.Closing;
        _process.StopProcess();
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
        _process.Dispose();
        _disposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private void ProcessExited(object? sender, EventArgs e)
    {
        if (State is not ProtocolSessionState.Closing and not ProtocolSessionState.Closed)
            State = ProtocolSessionState.Closed;
    }
}
