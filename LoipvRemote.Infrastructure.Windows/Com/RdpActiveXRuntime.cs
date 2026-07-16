using AxMSTSCLib;
using LoipvRemote.Domain.Protocols.Rdp;
using LoipvRemote.Protocols.Abstractions;
using MSTSCLib;
using System.ComponentModel;
using System.Windows.Forms;

namespace LoipvRemote.Infrastructure.Windows.Com;

public sealed class RdpActiveXRuntime : IRdpClient, IRdpCredentialClient, IRdpRuntimeClient, IRdpDisplayClient, IRdpEventClient, IManagedEmbeddedWindow, IDisposable
{
    private MsRdpClient6NotSafeForScripting? _client;
    private bool _eventsSubscribed;

    public RdpActiveXRuntime(RdpVersion version)
    {
        Version = version;
        Control = CreateControl(version);
    }

    public RdpVersion Version { get; }
    public AxHost Control { get; }
    public IntPtr WindowHandle => Control.IsHandleCreated ? Control.Handle : IntPtr.Zero;
    public bool IsAvailable => _client is not null && !Control.IsDisposed;
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
        get => !Control.Enabled;
        set => Control.Enabled = !value;
    }

    public event EventHandler? Connecting;
    public event EventHandler? Connected;
    public event EventHandler? LoginComplete;
    public event EventHandler<int>? FatalError;
    public event EventHandler<int>? Disconnected;
    public event EventHandler? IdleTimeout;
    public event EventHandler? LeaveFullScreen;

    private MsRdpClient6NotSafeForScripting Client =>
        _client ?? throw new InvalidOperationException("RDP ActiveX control is not initialized.");

    public void Initialize()
    {
        if (Control.Parent is null)
            throw new InvalidOperationException("The RDP ActiveX control must be hosted before initialization.");

        Control.CreateControl();
        _client = Control.GetOcx() as MsRdpClient6NotSafeForScripting
            ?? throw new InvalidOperationException("The RDP ActiveX control did not expose the expected client interface.");
    }

    public bool AttachTo(IntPtr parentWindowHandle, TimeSpan timeout)
    {
        if (parentWindowHandle == IntPtr.Zero || Control.IsDisposed)
            return false;

        System.Windows.Forms.Control? parent = System.Windows.Forms.Control.FromHandle(parentWindowHandle);
        if (parent is null || parent.IsDisposed)
            return false;

        if (ReferenceEquals(Control.Parent, parent))
            return true;

        AxHost initializer = Control;
        initializer.BeginInit();
        try
        {
            Control.Name = "RdpActiveX";
            Control.Dock = DockStyle.Fill;
            parent.Controls.Add(Control);
            Control.BringToFront();
        }
        finally
        {
            initializer.EndInit();
        }

        return ReferenceEquals(Control.Parent, parent);
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
        if (!Control.IsDisposed)
            Control.Focus();
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

    public bool GatewaySupported => Client.TransportSettings.GatewayIsSupported != 0;

    public void ConfigureGateway(uint usageMethod, string hostname, bool useSmartCard)
    {
        Client.TransportSettings.GatewayUsageMethod = usageMethod;
        Client.TransportSettings.GatewayHostname = hostname;
        Client.TransportSettings.GatewayProfileUsageMethod = 1;
        if (useSmartCard)
            Client.TransportSettings.GatewayCredsSource = 1;
    }

    public void SetGatewayCredentials(string username, string password, string domain)
    {
        Client.TransportSettings2.GatewayUsername = username;
        Client.TransportSettings2.GatewayPassword = password;
        Client.TransportSettings2.GatewayDomain = domain;
    }

    public void DisableGatewayCredentialSharing() =>
        Client.TransportSettings2.GatewayCredSharing = 0;

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

    public object? GetExtendedProperty(string property) =>
        ((IMsRdpExtendedSettings)Client).get_Property(property);

    public void SetExtendedProperty(string property, object value) =>
        ((IMsRdpExtendedSettings)Client).set_Property(property, ref value);

    public void ConfigureVersion7(
        uint audioQualityMode,
        bool redirectAudioCapture,
        uint networkConnectionType,
        bool useRedirectionServerName,
        string authenticationServiceClass,
        string pcb,
        string? encryptedGatewayToken)
    {
        MsRdpClient7NotSafeForScripting client = (MsRdpClient7NotSafeForScripting)Client;
        client.AdvancedSettings8.AudioQualityMode = audioQualityMode;
        client.AdvancedSettings8.AudioCaptureRedirectionMode = redirectAudioCapture;
        client.AdvancedSettings8.NetworkConnectionType = networkConnectionType;
        if (useRedirectionServerName && Control.GetOcx() is IMsRdpPreferredRedirectionInfo redirection)
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
        if (Client is MsRdpClient9NotSafeForScripting version9)
        {
            try
            {
                version9.UpdateSessionDisplaySettings(width, height, width, height, orientation, desktopScale, deviceScale);
                return;
            }
            catch
            {
            }
        }
        ((MsRdpClient8NotSafeForScripting)Client).Reconnect(width, height);
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

        IMsRdpClientNonScriptable5 nonScriptable = Control.GetOcx() as IMsRdpClientNonScriptable5
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
        Client.OnConnecting += ClientOnConnecting;
        Client.OnConnected += ClientOnConnected;
        Client.OnLoginComplete += ClientOnLoginComplete;
        Client.OnFatalError += ClientOnFatalError;
        Client.OnDisconnected += ClientOnDisconnected;
        Client.OnIdleTimeoutNotification += ClientOnIdleTimeout;
        Client.OnLeaveFullScreenMode += ClientOnLeaveFullScreen;
        _eventsSubscribed = true;
    }

    public void UnsubscribeEvents()
    {
        if (_client is null)
            return;
        if (!_eventsSubscribed)
            return;
        Client.OnConnecting -= ClientOnConnecting;
        Client.OnConnected -= ClientOnConnected;
        Client.OnLoginComplete -= ClientOnLoginComplete;
        Client.OnFatalError -= ClientOnFatalError;
        Client.OnDisconnected -= ClientOnDisconnected;
        Client.OnIdleTimeoutNotification -= ClientOnIdleTimeout;
        Client.OnLeaveFullScreenMode -= ClientOnLeaveFullScreen;
        _eventsSubscribed = false;
    }

    public static bool IsSupported(RdpVersion version)
    {
        try
        {
            using Panel host = new();
            _ = host.Handle;
            using RdpActiveXRuntime runtime = new(version);
            if (!runtime.AttachTo(host.Handle, TimeSpan.FromSeconds(1)))
                return false;
            runtime.Initialize();
            return runtime.IsAvailable;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        UnsubscribeEvents();
        Control.Dispose();
        _client = null;
    }

    private static AxHost CreateControl(RdpVersion version) => version switch
    {
        RdpVersion.Rdc6 => new AxMsRdpClient6NotSafeForScripting(),
        RdpVersion.Rdc7 => new AxMsRdpClient7NotSafeForScripting(),
        RdpVersion.Rdc8 => new AxMsRdpClient8NotSafeForScripting(),
        RdpVersion.Rdc9 => new AxMsRdpClient9NotSafeForScripting(),
        RdpVersion.Rdc10 => new AxMsRdpClient10NotSafeForScripting(),
        RdpVersion.Rdc11 => new AxMsRdpClient11NotSafeForScripting(),
        _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
    };

    private void ClientOnConnecting() => Connecting?.Invoke(this, EventArgs.Empty);
    private void ClientOnConnected() => Connected?.Invoke(this, EventArgs.Empty);
    private void ClientOnLoginComplete() => LoginComplete?.Invoke(this, EventArgs.Empty);
    private void ClientOnFatalError(int code) => FatalError?.Invoke(this, code);
    private void ClientOnDisconnected(int reason) => Disconnected?.Invoke(this, reason);
    private void ClientOnIdleTimeout() => IdleTimeout?.Invoke(this, EventArgs.Empty);
    private void ClientOnLeaveFullScreen() => LeaveFullScreen?.Invoke(this, EventArgs.Empty);
}
