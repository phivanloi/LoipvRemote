using LoipvRemote.Domain.Protocols.Rdp;
using LoipvRemote.Protocols.Abstractions;
using MSTSCLib;
using System.Runtime.InteropServices;

namespace LoipvRemote.Infrastructure.Windows.Com;

/// <summary>
/// Hosts the Microsoft RDP ActiveX control in a native ATL child HWND.
/// The control is supplied by Windows while this host owns the native child
/// window and message integration directly.
/// </summary>
public sealed class RdpActiveXRuntime : IRdpClient, IRdpCredentialClient, IRdpRuntimeClient, IRdpDisplayClient, IRdpEventClient, IManagedEmbeddedWindow, IDisposable
{
    private const int WsChild = unchecked((int)0x40000000);
    private const int WsVisible = unchecked((int)0x10000000);
    private const int WsClipChildren = 0x02000000;
    private const int WsClipSiblings = 0x04000000;
    private IMsRdpClient10? _client;
    private IMsTscAxEvents_Event? _events;
    private object? _comControl;
    private object? _atlHost;
    private IntPtr _windowHandle;
    private IntPtr _nativeParentWindowHandle;
    private bool _eventsSubscribed;
    private bool _disposed;

    public RdpActiveXRuntime(RdpVersion version) => Version = version;

    public RdpVersion Version { get; }
    public IntPtr WindowHandle => _windowHandle;
    public bool IsAvailable => _client is not null && _windowHandle != IntPtr.Zero && IsWindow(_windowHandle);
    public System.Version ClientVersion => new(Client.Version);
    public bool SmartSize
    {
        get => Client.AdvancedSettings2.SmartSizing;
        set => Client.AdvancedSettings2.SmartSizing = value;
    }
    public bool FullScreen
    {
        get => Client.FullScreen;
        set => Client.FullScreen = value;
    }
    public bool ViewOnly
    {
        get => !IsWindowEnabled(_windowHandle);
        set => _ = EnableWindow(_windowHandle, !value);
    }

    public event EventHandler? Connecting;
    public event EventHandler? Connected;
    public event EventHandler? LoginComplete;
    public event EventHandler<int>? FatalError;
    public event EventHandler<int>? Disconnected;
    public event EventHandler? IdleTimeout;
    public event EventHandler? LeaveFullScreen;

    private IMsRdpClient10 Client =>
        _client ?? throw new InvalidOperationException("RDP ActiveX control is not initialized.");

    private IMsTscAxEvents_Event Events =>
        _events ?? throw new InvalidOperationException("RDP ActiveX events are not initialized.");

    public void Initialize()
    {
        if (_windowHandle == IntPtr.Zero || _nativeParentWindowHandle == IntPtr.Zero)
            throw new InvalidOperationException("The RDP ActiveX control must be hosted before initialization.");
        if (_client is not null)
            return;

        int result = AtlAxGetHost(_windowHandle, out IntPtr hostUnknown);
        if (result < 0 || hostUnknown == IntPtr.Zero)
            throw new InvalidOperationException($"The RDP ActiveX host could not be initialized (HRESULT 0x{result:X8}).");

        try
        {
            _atlHost = Marshal.GetTypedObjectForIUnknown(hostUnknown, typeof(IAxWinHostWindow));
            Guid clientInterfaceId = typeof(IMsRdpClient10).GUID;
            result = ((IAxWinHostWindow)_atlHost).QueryControl(ref clientInterfaceId, out IntPtr clientUnknown);
            if (result < 0 || clientUnknown == IntPtr.Zero)
                throw new InvalidOperationException($"The RDP ActiveX host could not expose its client interface (HRESULT 0x{result:X8}).");

            try
            {
                _comControl = Marshal.GetTypedObjectForIUnknown(clientUnknown, typeof(IMsRdpClient10));
            }
            finally
            {
                Marshal.Release(clientUnknown);
            }
            _client = _comControl as IMsRdpClient10
                ?? throw new InvalidOperationException("The RDP ActiveX control did not expose the expected client interface.");
            _events = _comControl as IMsTscAxEvents_Event
                ?? throw new InvalidOperationException("The RDP ActiveX control did not expose its event interface.");
        }
        finally
        {
            Marshal.Release(hostUnknown);
        }
    }

    public bool AttachTo(IntPtr parentWindowHandle, TimeSpan timeout)
    {
        _ = timeout;
        if (parentWindowHandle == IntPtr.Zero)
            return false;
        if (_windowHandle != IntPtr.Zero)
        {
            if (_nativeParentWindowHandle == parentWindowHandle)
                return true;

            _ = SetParent(_windowHandle, parentWindowHandle);
            _nativeParentWindowHandle = GetParent(_windowHandle) == parentWindowHandle
                ? parentWindowHandle
                : IntPtr.Zero;
            return _nativeParentWindowHandle != IntPtr.Zero;
        }

        if (!AtlAxWinInit())
            return false;

        IntPtr handle = CreateWindowEx(
            0,
            "AtlAxWin",
            GetControlClassId(Version).ToString("B"),
            WsChild | WsVisible | WsClipChildren | WsClipSiblings,
            0,
            0,
            1,
            1,
            parentWindowHandle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        if (handle == IntPtr.Zero)
            return false;

        _windowHandle = handle;
        _nativeParentWindowHandle = parentWindowHandle;
        return true;
    }

    public void ConfigureEndpoint(string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        Client.Server = host;
        Client.AdvancedSettings2.RDPPort = port;
    }

    public void Connect() => Client.Connect();
    public void Disconnect() => Client.Disconnect();
    public void Focus()
    {
        if (_windowHandle != IntPtr.Zero)
            _ = SetFocus(_windowHandle);
    }

    public string GetErrorDescription(int disconnectReason) =>
        Client.GetErrorDescription((uint)disconnectReason, (uint)Client.ExtendedDisconnectReason);

    public void ApplyConfiguration(RdpRuntimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Client.Server = configuration.Server;
        Client.FullScreenTitle = configuration.FullScreenTitle;
        Client.AdvancedSettings2.MinutesToIdleTimeout = configuration.IdleTimeoutMinutes;
        Client.SecuredSettings2.StartProgram = configuration.StartProgram;
        Client.SecuredSettings2.WorkDir = configuration.WorkingDirectory;
        Client.AdvancedSettings2.GrabFocusOnConnect = true;
        Client.AdvancedSettings3.EnableAutoReconnect = true;
        Client.AdvancedSettings3.MaxReconnectAttempts = configuration.MaxReconnectAttempts;
        Client.AdvancedSettings2.keepAliveInterval = 60000;
        Client.AdvancedSettings5.AuthenticationLevel = configuration.AuthenticationLevel;
        Client.AdvancedSettings2.EncryptionEnabled = 1;
        Client.AdvancedSettings2.overallConnectionTimeout = configuration.OverallConnectionTimeout;
        Client.AdvancedSettings2.BitmapPeristence = Convert.ToInt32(configuration.CacheBitmaps);
        Client.AdvancedSettings7.EnableCredSspSupport = configuration.EnableCredSsp;
        Client.AdvancedSettings7.ConnectToAdministerServer = configuration.ConnectToAdministerServer;
        if (configuration.Port != 3389)
            Client.AdvancedSettings2.RDPPort = configuration.Port;
        if (configuration.RedirectKeys)
            Client.SecuredSettings2.KeyboardHookMode = 1;
        Client.AdvancedSettings2.RedirectPorts = configuration.RedirectPorts;
        Client.AdvancedSettings2.RedirectPrinters = configuration.RedirectPrinters;
        Client.AdvancedSettings2.RedirectSmartCards = configuration.RedirectSmartCards;
        Client.SecuredSettings2.AudioRedirectionMode = configuration.AudioRedirectionMode;
        Client.AdvancedSettings6.RedirectClipboard = configuration.RedirectClipboard;
        ApplyDriveRedirection(configuration.DriveRedirection, configuration.CustomDrives);
        Client.AdvancedSettings2.PerformanceFlags = configuration.PerformanceFlags;
        Client.AdvancedSettings2.LoadBalanceInfo = configuration.LoadBalanceInfo;
        Client.ColorDepth = configuration.ColorDepth;
        Client.ConnectingText = configuration.ConnectingText;
        ViewOnly = configuration.ViewOnly;
    }

    public void ApplyCredentials(RdpCredentialConfiguration credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        Client.UserName = credentials.Username;
        Client.Domain = credentials.Domain;
        if (credentials.AssignPassword && !string.IsNullOrEmpty(credentials.Password))
            Client.AdvancedSettings2.ClearTextPassword = credentials.Password;
    }

    public bool ApplyGateway(RdpGatewayConfiguration gateway)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        if (Client.TransportSettings.GatewayIsSupported == 0)
            return false;
        if (!gateway.Enabled)
            return true;

        Client.TransportSettings.GatewayUsageMethod = gateway.UsageMethod;
        Client.TransportSettings.GatewayHostname = gateway.Hostname;
        Client.TransportSettings.GatewayProfileUsageMethod = 1;
        if (gateway.UseSmartCard)
            Client.TransportSettings.GatewayCredsSource = 1;
        if (gateway.DisableCredentialSharing)
            Client.TransportSettings2.GatewayCredSharing = 0;
        if (!string.IsNullOrEmpty(gateway.Username))
        {
            Client.TransportSettings2.GatewayUsername = gateway.Username;
            Client.TransportSettings2.GatewayPassword = gateway.Password;
            Client.TransportSettings2.GatewayDomain = gateway.Domain;
        }
        return true;
    }

    public void ApplyDisplay(RdpDisplayConfiguration display)
    {
        ArgumentNullException.ThrowIfNull(display);
        SetExtendedProperty("DesktopScaleFactor", display.DesktopScaleFactor);
        SetExtendedProperty("DeviceScaleFactor", display.DeviceScaleFactor);
        Client.FullScreen = display.FullScreen;
        Client.DesktopWidth = display.Width;
        Client.DesktopHeight = display.Height;
        Client.AdvancedSettings2.SmartSizing = display.SmartSize;
    }

    public object? GetExtendedProperty(string property) => ((IMsRdpExtendedSettings)Client).get_Property(property);
    public void SetExtendedProperty(string property, object value) => ((IMsRdpExtendedSettings)Client).set_Property(property, ref value);

    public void ConfigureVersion7(uint audioQualityMode, bool redirectAudioCapture, uint networkConnectionType, bool useRedirectionServerName, string authenticationServiceClass, string pcb, string? encryptedGatewayToken)
    {
        IMsRdpClient10 client = Client;
        client.AdvancedSettings8.AudioQualityMode = audioQualityMode;
        client.AdvancedSettings8.AudioCaptureRedirectionMode = redirectAudioCapture;
        client.AdvancedSettings8.NetworkConnectionType = networkConnectionType;
        if (useRedirectionServerName && Client is IMsRdpPreferredRedirectionInfo redirection)
            redirection.UseRedirectionServerName = true;
        if (!string.IsNullOrEmpty(authenticationServiceClass))
        {
            SetExtendedProperty("DisableCredentialsDelegation", true);
            client.AdvancedSettings7.AuthenticationServiceClass = authenticationServiceClass;
            client.AdvancedSettings8.EnableCredSspSupport = true;
            client.AdvancedSettings8.NegotiateSecurityLayer = false;
            client.AdvancedSettings7.PCB = pcb;
        }
        if (encryptedGatewayToken is { Length: > 0 })
        {
            client.TransportSettings3.GatewayEncryptedAuthCookie = encryptedGatewayToken;
            client.TransportSettings3.GatewayEncryptedAuthCookieSize = (uint)encryptedGatewayToken.Length;
            client.TransportSettings3.GatewayCredsSource = 5;
        }
    }

    public void ResizeSession(uint width, uint height, uint orientation, uint desktopScale, uint deviceScale)
    {
        Client.UpdateSessionDisplaySettings(width, height, width, height, orientation, desktopScale, deviceScale);
    }

    private void ApplyDriveRedirection(RdpDriveRedirection mode, string customDrives)
    {
        if (mode == RdpDriveRedirection.None)
        {
            Client.AdvancedSettings2.RedirectDrives = false;
            return;
        }
        if (mode == RdpDriveRedirection.All)
        {
            Client.AdvancedSettings2.RedirectDrives = true;
            return;
        }

        IMsRdpClientNonScriptable5 nonScriptable = Client as IMsRdpClientNonScriptable5
            ?? throw new InvalidOperationException("The RDP ActiveX control does not expose drive redirection settings.");
        HashSet<char> localFixedDrives = DriveInfo.GetDrives()
            .Where(drive => drive.DriveType == DriveType.Fixed)
            .Select(drive => char.ToUpperInvariant(drive.Name[0]))
            .ToHashSet();
        for (uint index = 0; index < nonScriptable.DriveCollection.DriveCount; index++)
        {
            IMsRdpDrive? drive = nonScriptable.DriveCollection.DriveByIndex[index];
            if (drive is null)
                continue;
            char letter = char.ToUpperInvariant(drive.Name[0]);
            drive.RedirectionState = mode == RdpDriveRedirection.Custom
                ? customDrives.Contains(letter.ToString(), StringComparison.OrdinalIgnoreCase)
                : localFixedDrives.Contains(letter);
        }
    }

    public void SubscribeEvents()
    {
        if (_eventsSubscribed)
            return;
        Events.OnConnecting += ClientOnConnecting;
        Events.OnConnected += ClientOnConnected;
        Events.OnLoginComplete += ClientOnLoginComplete;
        Events.OnFatalError += ClientOnFatalError;
        Events.OnDisconnected += ClientOnDisconnected;
        Events.OnIdleTimeoutNotification += ClientOnIdleTimeout;
        Events.OnLeaveFullScreenMode += ClientOnLeaveFullScreen;
        _eventsSubscribed = true;
    }

    public void UnsubscribeEvents()
    {
        if (_client is null || !_eventsSubscribed)
            return;
        Events.OnConnecting -= ClientOnConnecting;
        Events.OnConnected -= ClientOnConnected;
        Events.OnLoginComplete -= ClientOnLoginComplete;
        Events.OnFatalError -= ClientOnFatalError;
        Events.OnDisconnected -= ClientOnDisconnected;
        Events.OnIdleTimeoutNotification -= ClientOnIdleTimeout;
        Events.OnLeaveFullScreenMode -= ClientOnLeaveFullScreen;
        _eventsSubscribed = false;
    }

    public static bool IsSupported(RdpVersion version)
    {
        try
        {
            using NativeProbeHost host = new();
            using RdpActiveXRuntime runtime = new(version);
            return runtime.AttachTo(host.Handle, TimeSpan.Zero) && InitializeForProbe(runtime);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        UnsubscribeEvents();
        // The ATL host owns the ActiveX instance. Destroy its HWND before
        // releasing the COM wrappers so native window teardown cannot send
        // messages back to a control whose RCW is already gone.
        if (_windowHandle != IntPtr.Zero)
            _ = DestroyWindow(_windowHandle);
        _windowHandle = IntPtr.Zero;
        _nativeParentWindowHandle = IntPtr.Zero;
        _client = null;
        _events = null;
        if (_comControl is not null && Marshal.IsComObject(_comControl))
            _ = Marshal.FinalReleaseComObject(_comControl);
        _comControl = null;
        if (_atlHost is not null && Marshal.IsComObject(_atlHost))
            _ = Marshal.FinalReleaseComObject(_atlHost);
        _atlHost = null;
        GC.SuppressFinalize(this);
    }

    private static bool InitializeForProbe(RdpActiveXRuntime runtime)
    {
        runtime.Initialize();
        return runtime.IsAvailable;
    }

    private static Guid GetControlClassId(RdpVersion version) => version switch
    {
        RdpVersion.Rdc10 => typeof(MsRdpClient10NotSafeForScriptingClass).GUID,
        RdpVersion.Rdc11 => typeof(MsRdpClient11NotSafeForScriptingClass).GUID,
        _ => throw new PlatformNotSupportedException($"RDP ActiveX version '{version}' is retired. WinUI supports Rdc10 and Rdc11 only.")
    };

    private void ClientOnConnecting() => Connecting?.Invoke(this, EventArgs.Empty);
    private void ClientOnConnected() => Connected?.Invoke(this, EventArgs.Empty);
    private void ClientOnLoginComplete() => LoginComplete?.Invoke(this, EventArgs.Empty);
    private void ClientOnFatalError(int code) => FatalError?.Invoke(this, code);
    private void ClientOnDisconnected(int reason) => Disconnected?.Invoke(this, reason);
    private void ClientOnIdleTimeout() => IdleTimeout?.Invoke(this, EventArgs.Empty);
    private void ClientOnLeaveFullScreen() => LeaveFullScreen?.Invoke(this, EventArgs.Empty);

    [DllImport("atl.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AtlAxWinInit();

    [DllImport("atl.dll", PreserveSig = true)]
    private static extern int AtlAxGetHost(IntPtr windowHandle, out IntPtr unknown);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int extendedStyle, string className, string windowName, int style, int x, int y, int width, int height, IntPtr parentWindowHandle, IntPtr menu, IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr childWindowHandle, IntPtr parentWindowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetParent(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr windowHandle, [MarshalAs(UnmanagedType.Bool)] bool enabled);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr windowHandle);

    private sealed class NativeProbeHost : IDisposable
    {
        private const int WsChild = unchecked((int)0x40000000);
        public NativeProbeHost()
        {
            Handle = CreateWindowEx(0, "STATIC", string.Empty, WsChild, 0, 0, 1, 1, GetDesktopWindow(), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (Handle == IntPtr.Zero)
                throw new InvalidOperationException("Could not create the RDP capability probe host.");
        }

        public IntPtr Handle { get; }
        public void Dispose() => _ = DestroyWindow(Handle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
    }
}

[ComImport]
[Guid("B6EA2050-048A-11D1-82B9-00C04FB9942E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAxWinHostWindow
{
    [PreserveSig]
    int CreateControl([MarshalAs(UnmanagedType.LPWStr)] string data, IntPtr windowHandle, IntPtr stream);

    [PreserveSig]
    int CreateControlEx(
        [MarshalAs(UnmanagedType.LPWStr)] string data,
        IntPtr windowHandle,
        IntPtr stream,
        out IntPtr control,
        ref Guid adviseInterface,
        IntPtr adviseSink);

    [PreserveSig]
    int AttachControl(IntPtr control, IntPtr windowHandle);

    [PreserveSig]
    int QueryControl(ref Guid interfaceId, out IntPtr control);
}
