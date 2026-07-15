using LoipvRemote.Desktop.Sessions;
using LoipvRemote.Connection;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.UI.Tabs;
using System.Runtime.Versioning;

namespace LoipvRemote.UI.Adapters;

/// <summary>
/// Executable-side UI adapter for the desktop session host. Protocol lifecycle
/// and embedded-window handling live in <see cref="DesktopSessionHost"/>;
/// this adapter only connects that host to the WinForms surface and
/// exposes connection-specific presentation capabilities to the shell.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ProtocolSessionBridge(
    ConnectionDefinition definition,
    IProtocolSession session) : IInputMessageTarget, IRemoteScreenController, IRemoteSpecialKeysController,
    IViewOnlySession, ISmartSizingSession, IFullscreenSession, IPuttySettingsSession, IProtocolSession
{
    private readonly DesktopSessionHost _host = new(definition, session);
    private IProtocolSessionEvents? _sessionEvents;

    public InterfaceControl? InterfaceControl
    {
        get => _host.Surface as InterfaceControl;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _host.AttachSurface(value);
        }
    }

    public ConnectionInfo.Force Force { get; set; }
    public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;
    public ProtocolCapabilities Capabilities => _host.Capabilities;

    public bool Initialize()
    {
        try
        {
            if (!_host.InitializeSurface() || !_host.InitializeSession())
            {
                State = ProtocolSessionState.Faulted;
                return false;
            }

            State = ProtocolSessionState.Initialized;
            SubscribeSessionEvents();
            return true;
        }
        catch (Exception exception)
        {
            State = ProtocolSessionState.Faulted;
            ErrorOccured?.Invoke(this, exception.Message, null);
            return false;
        }
    }

    public bool Connect()
    {
        if (State != ProtocolSessionState.Initialized || !_host.Connect())
        {
            State = ProtocolSessionState.Faulted;
            return false;
        }

        if (_sessionEvents is null)
        {
            State = ProtocolSessionState.Connected;
            Connected?.Invoke(this);
        }
        return true;
    }

    public void Disconnect()
    {
        _host.Disconnect();
        State = ProtocolSessionState.Closing;
    }

    public void Focus()
    {
        // Delayed focus callbacks can run after a rapid tab switch. The shell
        // must never reactivate the previous embedded process in that case.
        if (_host.Surface is InterfaceControl interfaceControl &&
            interfaceControl.FindForm() is ConnectionTab tab &&
            !ReferenceEquals(tab.DockPanel?.ActiveDocument, tab))
            return;

        _host.Focus();
    }

    public void Close()
    {
        if (State == ProtocolSessionState.Closed)
            return;

        State = ProtocolSessionState.Closing;
        try
        {
            UnsubscribeSessionEvents();
            _host.Close();
        }
        finally
        {
            State = ProtocolSessionState.Closed;
            Closed?.Invoke(this);
        }
    }

    public void Dispose()
    {
        UnsubscribeSessionEvents();
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    public bool TryForwardInputMessage(int message, IntPtr wParam, IntPtr lParam) =>
        _host.TryForwardInputMessage(message, wParam, lParam);

    public void RefreshScreen() =>
        (_host.Session as IRemoteScreenController)?.RefreshScreen();

    public void SendSpecialKeys(RemoteSpecialKey key) =>
        (_host.Session as IRemoteSpecialKeysController)?.SendSpecialKeys(key);

    public bool ViewOnly
    {
        get => (_host.Session as IViewOnlySession)?.ViewOnly ?? false;
        set
        {
            if (_host.Session is IViewOnlySession viewOnly)
                viewOnly.ViewOnly = value;
        }
    }

    public void ToggleViewOnly() => (_host.Session as IViewOnlySession)?.ToggleViewOnly();

    public bool SmartSize
    {
        get => (_host.Session as ISmartSizingSession)?.SmartSize ?? false;
        set
        {
            if (_host.Session is ISmartSizingSession smartSize)
                smartSize.SmartSize = value;
        }
    }

    public void ToggleSmartSize() => (_host.Session as ISmartSizingSession)?.ToggleSmartSize();

    public bool Fullscreen
    {
        get => (_host.Session as IFullscreenSession)?.Fullscreen ?? false;
        set
        {
            if (_host.Session is IFullscreenSession fullscreen)
                fullscreen.Fullscreen = value;
        }
    }

    public void ToggleFullscreen() => (_host.Session as IFullscreenSession)?.ToggleFullscreen();

    public void ShowSettingsDialog() => (_host.Session as IPuttySettingsSession)?.ShowSettingsDialog();

    public delegate void ConnectingHandler(object sender);
    public delegate void ConnectedHandler(object sender);
    public delegate void DisconnectedHandler(object sender, string disconnectedMessage, int? reasonCode);
    public delegate void ProtocolErrorHandler(object sender, string errorMessage, int? errorCode);
    public delegate void ClosingHandler(object sender);
    public delegate void ClosedHandler(object sender);
    public delegate void TitleChangedHandler(object sender, string newTitle);

    public event ConnectingHandler? Connecting;
    public event ConnectedHandler? Connected;
    public event DisconnectedHandler? Disconnected;
    public event ProtocolErrorHandler? ErrorOccured;
    public event ClosingHandler? Closing;
    public event ClosedHandler? Closed;
    public event TitleChangedHandler? TitleChanged;

    public void RaiseConnecting() => Connecting?.Invoke(this);
    public void RaiseClosing() => Closing?.Invoke(this);
    public void RaiseDisconnected(string message, int? reasonCode) => Disconnected?.Invoke(this, message, reasonCode);
    public void RaiseError(string message, int? errorCode) => ErrorOccured?.Invoke(this, message, errorCode);
    public void RaiseTitleChanged(string title) => TitleChanged?.Invoke(this, title);

    private void SubscribeSessionEvents()
    {
        if (_sessionEvents is not null || _host.Session is not IProtocolSessionEvents events)
            return;

        _sessionEvents = events;
        events.Connecting += OnSessionConnecting;
        events.Connected += OnSessionConnected;
        events.Disconnected += OnSessionDisconnected;
        events.ErrorOccurred += OnSessionErrorOccurred;
    }

    private void UnsubscribeSessionEvents()
    {
        if (_sessionEvents is null)
            return;

        _sessionEvents.Connecting -= OnSessionConnecting;
        _sessionEvents.Connected -= OnSessionConnected;
        _sessionEvents.Disconnected -= OnSessionDisconnected;
        _sessionEvents.ErrorOccurred -= OnSessionErrorOccurred;
        _sessionEvents = null;
    }

    private void OnSessionConnecting(object? sender, EventArgs args) => RaiseConnecting();

    private void OnSessionConnected(object? sender, EventArgs args)
    {
        State = ProtocolSessionState.Connected;
        Connected?.Invoke(this);
    }

    private void OnSessionDisconnected(object? sender, ProtocolSessionDisconnectedEventArgs args)
    {
        State = ProtocolSessionState.Closed;
        RaiseDisconnected(args.Message, args.Code);
    }

    private void OnSessionErrorOccurred(object? sender, ProtocolSessionErrorEventArgs args)
    {
        State = ProtocolSessionState.Faulted;
        RaiseError(args.Message, args.Code);
    }
}
