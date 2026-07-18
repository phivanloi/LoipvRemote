using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;
using System.Diagnostics;

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
    private bool _windowShown;
    private bool _focusActivated;
    private PuttyDpiSettingsSession? _dpiSettingsSession;
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

        PuttyLaunchOptions launchOptions = _options.LaunchOptions;
        if (OperatingSystem.IsWindows())
            _dpiSettingsSession = PuttyDpiSettingsSession.TryCreate(launchOptions.SavedSession, _hostWindowHandle);
        if (_dpiSettingsSession is not null)
            launchOptions = launchOptions with { SavedSession = _dpiSettingsSession.SessionName };

        bool started = _process.Start(
            new PuttyProcessStartOptions(
                _options.ExecutablePath,
                PuttyLaunchArguments.Build(launchOptions),
                _options.StartMinimized,
                StartHidden: false),
            ProcessExited);
        if (!started)
        {
            _dpiSettingsSession?.Dispose();
            _dpiSettingsSession = null;
        }
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
        EnsureWindowShown(windowHandle);
        EnsureBorderlessChildStyle(windowHandle);
        if (ownerWindowHandle != IntPtr.Zero &&
            _windowOperations.TryFocus(ownerWindowHandle, windowHandle))
            return;

        if (!_focusActivated)
        {
            _windowOperations.Activate(windowHandle);
            _focusActivated = true;
        }

        _windowOperations.SetFocus(windowHandle);
    }

    public bool AttachTo(IntPtr parentWindowHandle, TimeSpan timeout)
    {
        if (!IsAvailable || parentWindowHandle == IntPtr.Zero)
            return false;

        // PuTTY versions installed in the field do not consistently support
        // the undocumented -hwndparent argument. Start it normally, then wait
        // briefly for its real top-level HWND before applying SetParent.
        Stopwatch stopwatch = Stopwatch.StartNew();
        nint windowHandle;
        do
        {
            windowHandle = WindowHandle;
            if (windowHandle != IntPtr.Zero)
                break;

            Thread.Sleep(50);
        }
        while (stopwatch.Elapsed < timeout && IsAvailable);

        if (!IsAvailable || windowHandle == IntPtr.Zero)
            return false;

        // PuTTY is a normal top-level window.  SetParent alone leaves its
        // caption, system menu and resize buttons visible and the window keeps
        // its old screen position.  Convert it to a real child before parenting
        // it, then force Windows to recalculate the non-client frame.
        // PuTTY starts as a normal top-level window. The desktop host converts
        // it to a borderless child exactly once for each target surface.
        if (_attachedParentWindowHandle != parentWindowHandle ||
            _attachedWindowHandle != windowHandle)
        {
            AttachWindow(windowHandle, parentWindowHandle);
        }
        _embeddedWindowHandle = windowHandle;
        _attachedWindowHandle = windowHandle;
        // Show only when attaching a new PuTTY HWND. Re-showing it during
        // every tab focus or bounds refresh causes a visible flash.
        EnsureWindowShown(windowHandle);
        // PuTTY can restore its top-level non-client style while handling the
        // asynchronous show request. Apply the borderless child style only
        // after showing it so the caption and resize frame cannot return.
        SetBorderlessChildStyle(windowHandle, refreshFrame: true);
        return true;
    }

    private nint FindHostedWindow()
    {
        if (_hostWindowHandle == IntPtr.Zero || !_process.IsRunning || _process.ProcessId <= 0)
            return IntPtr.Zero;

        IntPtr afterHandle = IntPtr.Zero;
        for (int i = 0; i < 32; i++)
        {
            IntPtr candidate = _windowOperations.FindChildWindow(_hostWindowHandle, afterHandle);
            if (candidate == IntPtr.Zero)
                return IntPtr.Zero;
            if (_windowOperations.HasClassName(candidate, "PuTTY") &&
                _windowOperations.GetWindowProcessId(candidate) == (uint)_process.ProcessId)
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
            EmbeddedWindowBounds viewportBounds = PuttyEmbeddedWindowLayout.CreateViewportBounds(bounds);
            _windowOperations.Move(
                windowHandle,
                viewportBounds.X,
                viewportBounds.Y,
                viewportBounds.Width,
                viewportBounds.Height);
            // SetWindowPos with SWP_SHOWWINDOW can cause PuTTY to restore its
            // top-level caption. Remove chrome after that final show/move.
            EnsureBorderlessChildStyle(windowHandle);
        }
    }

    private void SetBorderlessChildStyle(nint windowHandle, bool refreshFrame)
    {
        int borderlessChildStyle = PuttyEmbeddedWindowLayout.CreateBorderlessChildStyle(
            _windowOperations.GetWindowStyle(windowHandle));
        _windowOperations.TrySetWindowStyle(windowHandle, borderlessChildStyle);
        int borderlessExtendedStyle = PuttyEmbeddedWindowLayout.CreateBorderlessChildExtendedStyle(
            _windowOperations.GetWindowExtendedStyle(windowHandle));
        _windowOperations.TrySetWindowExtendedStyle(windowHandle, borderlessExtendedStyle);
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

    private void EnsureWindowShown(nint windowHandle)
    {
        if (_windowShown)
            return;

        _windowOperations.Show(windowHandle);
        _windowShown = true;
    }

    private void AttachWindow(nint windowHandle, nint parentWindowHandle)
    {
        SetBorderlessChildStyle(windowHandle, refreshFrame: false);
        _windowOperations.SetParent(windowHandle, parentWindowHandle);
        _attachedParentWindowHandle = parentWindowHandle;
        _windowShown = false;
        _focusActivated = false;
    }

    private void EnsureBorderlessChildStyle(nint windowHandle)
    {
        int currentStyle = _windowOperations.GetWindowStyle(windowHandle);
        int borderlessChildStyle = PuttyEmbeddedWindowLayout.CreateBorderlessChildStyle(currentStyle);
        if (currentStyle == borderlessChildStyle)
            return;

        _windowOperations.TrySetWindowStyle(windowHandle, borderlessChildStyle);
        int currentExtendedStyle = _windowOperations.GetWindowExtendedStyle(windowHandle);
        int borderlessExtendedStyle = PuttyEmbeddedWindowLayout.CreateBorderlessChildExtendedStyle(currentExtendedStyle);
        if (currentExtendedStyle != borderlessExtendedStyle)
            _windowOperations.TrySetWindowExtendedStyle(windowHandle, borderlessExtendedStyle);
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
        _windowShown = false;
        _focusActivated = false;
        _dpiSettingsSession?.Dispose();
        _dpiSettingsSession = null;
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
        _dpiSettingsSession?.Dispose();
        _dpiSettingsSession = null;
        if (State is not ProtocolSessionState.Closing and not ProtocolSessionState.Closed)
            State = ProtocolSessionState.Closed;
    }
}
