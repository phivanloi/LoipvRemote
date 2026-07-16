using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Putty;

/// <summary>PuTTY lifecycle, embedding and keyboard routing owned by the protocol module.</summary>
public sealed class PuttyProtocolSession : IProtocolSession, IEmbeddedWindow, IEmbeddedWindowHost, IInputMessageTarget, IPuttySettingsSession
{
    private readonly IPuttyProcessHost _process;
    private readonly IEmbeddedWindowOperations _windowOperations;
    private readonly PuttyConnectionOptions _options;
    // Process.MainWindowHandle becomes zero as soon as Windows re-parents a
    // top-level PuTTY window.  Keep the HWND captured before SetParent so the
    // rest of the session can still remove chrome, size and focus that window.
    private nint _embeddedWindowHandle;
    private nint _hostWindowHandle;
    private nint _attachedParentWindowHandle;
    private nint _attachedWindowHandle;
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
    public nint WindowHandle
    {
        get
        {
            // PuTTY can recreate its top-level window while the process stays
            // alive (for example after a reconnect or tab activation). Refresh
            // the hosted child lookup before falling back to the captured HWND
            // so keyboard/focus and layout never target a stale window handle.
            nint hostedWindow = FindHostedWindow();
            if (hostedWindow != IntPtr.Zero)
            {
                _embeddedWindowHandle = hostedWindow;
                return hostedWindow;
            }

            return _embeddedWindowHandle != IntPtr.Zero
                ? _embeddedWindowHandle
                : _process.MainWindowHandle;
        }
    }
    public string WindowTitle => _process.MainWindowTitle;

    public void SetHostWindowHandle(IntPtr parentWindowHandle)
    {
        if (parentWindowHandle == IntPtr.Zero)
            throw new ArgumentException("A non-zero parent window handle is required.", nameof(parentWindowHandle));
        if (_process.IsRunning)
            throw new InvalidOperationException("The PuTTY parent window must be set before the process starts.");

        _hostWindowHandle = parentWindowHandle;
    }

    private bool InitializeCore()
    {
        if (State is not ProtocolSessionState.Created and not ProtocolSessionState.Closed)
            return false;

        State = ProtocolSessionState.Initialized;
        return true;
    }

    public ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(InitializeCore());
    }

    private bool ConnectCore()
    {
        if (State != ProtocolSessionState.Initialized)
            return false;

        PuttyLaunchOptions launchOptions = _options.LaunchOptions with { ParentWindowHandle = _hostWindowHandle };
        bool started = _process.Start(
            new PuttyProcessStartOptions(_options.ExecutablePath, PuttyLaunchArguments.Build(launchOptions), _options.StartMinimized),
            ProcessExited);
        State = started ? ProtocolSessionState.Connected : ProtocolSessionState.Faulted;
        return started;
    }

    public ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ConnectCore());
    }

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CloseCore();
        return ValueTask.CompletedTask;
    }

    public void Focus()
        => Focus(IntPtr.Zero);

    public void Focus(IntPtr ownerWindowHandle)
    {
        nint windowHandle = WindowHandle;
        if (!IsAvailable || windowHandle == IntPtr.Zero)
            return;

        EnsureAttachedWindow(windowHandle);
        EnsureBorderlessChildStyle(windowHandle);
        _windowOperations.Show(windowHandle);
        _windowOperations.Activate(windowHandle);
        if (ownerWindowHandle != IntPtr.Zero &&
            _windowOperations.TryFocus(ownerWindowHandle, windowHandle))
            return;

        _windowOperations.SetFocus(windowHandle);
    }

    public bool AttachTo(IntPtr parentWindowHandle, TimeSpan timeout)
    {
        nint windowHandle = WindowHandle;
        if (!IsAvailable || parentWindowHandle == IntPtr.Zero || windowHandle == IntPtr.Zero)
            return false;

        // PuTTY is a normal top-level window.  SetParent alone leaves its
        // caption, system menu and resize buttons visible and the window keeps
        // its old screen position.  Convert it to a real child before parenting
        // it, then force Windows to recalculate the non-client frame.
        // The launch-time -hwndparent argument is only a hint to PuTTY.  PuTTY
        // can still create a top-level window with its original chrome, so the
        // desktop host must perform an explicit SetParent exactly once for
        // each target surface.  Comparing with _hostWindowHandle incorrectly
        // skipped this step whenever the launch hint and target were equal.
        if (_attachedParentWindowHandle != parentWindowHandle ||
            _attachedWindowHandle != windowHandle)
        {
            AttachWindow(windowHandle, parentWindowHandle);
        }
        _embeddedWindowHandle = windowHandle;
        _attachedWindowHandle = windowHandle;
        // PuTTY may restore its top-level style after SetParent. Reapply the
        // child style immediately, and also from Focus/Resize so a later
        // asynchronous style change cannot expose a caption or system buttons.
        SetBorderlessChildStyle(windowHandle, refreshFrame: true);
        _windowOperations.Show(windowHandle);
        return true;
    }

    private nint FindHostedWindow()
    {
        if (_hostWindowHandle == IntPtr.Zero || !_process.IsRunning)
            return IntPtr.Zero;

        IntPtr afterHandle = IntPtr.Zero;
        for (int i = 0; i < 32; i++)
        {
            IntPtr candidate = _windowOperations.FindChildWindow(_hostWindowHandle, afterHandle);
            if (candidate == IntPtr.Zero)
                return IntPtr.Zero;
            if (_windowOperations.HasClassName(candidate, "PuTTY"))
                return candidate;
            afterHandle = candidate;
        }

        return IntPtr.Zero;
    }

    public void Resize(EmbeddedWindowBounds bounds)
    {
        nint windowHandle = WindowHandle;
        if (IsAvailable && windowHandle != IntPtr.Zero && bounds.IsValid)
        {
            EnsureAttachedWindow(windowHandle);
            EnsureBorderlessChildStyle(windowHandle);
            _windowOperations.Show(windowHandle);
            _windowOperations.Move(
                windowHandle,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height);
        }
    }

    private void SetBorderlessChildStyle(nint windowHandle, bool refreshFrame)
    {
        int borderlessChildStyle = PuttyEmbeddedWindowLayout.CreateBorderlessChildStyle(
            _windowOperations.GetWindowStyle(windowHandle));
        _windowOperations.TrySetWindowStyle(windowHandle, borderlessChildStyle);
        if (refreshFrame)
            _windowOperations.RefreshFrame(windowHandle);
    }

    private void EnsureAttachedWindow(nint windowHandle)
    {
        if (_attachedParentWindowHandle == IntPtr.Zero ||
            _attachedWindowHandle == windowHandle)
            return;

        AttachWindow(windowHandle, _attachedParentWindowHandle);
        _embeddedWindowHandle = windowHandle;
        _attachedWindowHandle = windowHandle;
    }

    private void AttachWindow(nint windowHandle, nint parentWindowHandle)
    {
        SetBorderlessChildStyle(windowHandle, refreshFrame: false);
        _windowOperations.SetParent(windowHandle, parentWindowHandle);
        _attachedParentWindowHandle = parentWindowHandle;
    }

    private void EnsureBorderlessChildStyle(nint windowHandle)
    {
        int currentStyle = _windowOperations.GetWindowStyle(windowHandle);
        int borderlessChildStyle = PuttyEmbeddedWindowLayout.CreateBorderlessChildStyle(currentStyle);
        if (currentStyle == borderlessChildStyle)
            return;

        _windowOperations.TrySetWindowStyle(windowHandle, borderlessChildStyle);
        _windowOperations.RefreshFrame(windowHandle);
    }

    public bool TryForwardInputMessage(int message, IntPtr wParam, IntPtr lParam)
    {
        nint windowHandle = WindowHandle;
        if (!IsAvailable || windowHandle == IntPtr.Zero || !PuttyInputMessageRouter.ShouldForward(message))
            return false;

        EnsureAttachedWindow(windowHandle);
        _windowOperations.SendMessage(windowHandle, (uint)message, wParam, lParam);
        return true;
    }

    public void ShowSettingsDialog()
    {
        nint windowHandle = WindowHandle;
        if (IsAvailable && windowHandle != IntPtr.Zero)
            _windowOperations.ShowSettingsDialog(windowHandle, 0x50);
    }

    private void CloseCore()
    {
        if (State == ProtocolSessionState.Closed)
            return;

        State = ProtocolSessionState.Closing;
        _process.StopProcess();
        _embeddedWindowHandle = IntPtr.Zero;
        _attachedParentWindowHandle = IntPtr.Zero;
        _attachedWindowHandle = IntPtr.Zero;
        State = ProtocolSessionState.Closed;
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CloseCore();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        CloseCore();
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
        _embeddedWindowHandle = IntPtr.Zero;
        if (State is not ProtocolSessionState.Closing and not ProtocolSessionState.Closed)
            State = ProtocolSessionState.Closed;
    }
}
