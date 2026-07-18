using LoipvRemote.Domain.Protocols.Rdp;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Infrastructure.Windows.Com;

/// <summary>
/// ARM64 build surface for RDP. The Microsoft Terminal Services ActiveX
/// control is x86-only, so ARM64 must fail explicitly instead of producing an
/// invalid mixed-architecture binary.
/// </summary>
public sealed class RdpActiveXRuntime(RdpVersion version) :
    IRdpClient,
    IRdpCredentialClient,
    IRdpRuntimeClient,
    IRdpDynamicDisplayClient,
    IRdpDisplayClient,
    IRdpEventClient,
    IManagedEmbeddedWindow,
    IDisposable
{
    public RdpVersion Version { get; } = version;
    public bool IsAvailable => false;
    public IntPtr WindowHandle => IntPtr.Zero;
    public bool SmartSize { get; set; }
    public bool FullScreen { get; set; }
    public bool ViewOnly { get; set; }
    event EventHandler? IRdpEventClient.Connecting { add { } remove { } }
    event EventHandler? IRdpEventClient.Connected { add { } remove { } }
    event EventHandler? IRdpEventClient.LoginComplete { add { } remove { } }
    event EventHandler<int>? IRdpEventClient.FatalError { add { } remove { } }
    event EventHandler<int>? IRdpEventClient.Disconnected { add { } remove { } }
    event EventHandler? IRdpEventClient.IdleTimeout { add { } remove { } }
    event EventHandler? IRdpEventClient.LeaveFullScreen { add { } remove { } }

    public static bool IsSupported(RdpVersion _) => false;
    public void Initialize() => ThrowNotSupported();
    public void ConfigureEndpoint(string host, int port)
    {
        _ = host;
        _ = port;
        ThrowNotSupported();
    }
    public void Connect() => ThrowNotSupported();
    public void Disconnect()
    {
    }

    public void ApplyCredentials(RdpCredentialConfiguration credentials)
    {
        _ = credentials;
        ThrowNotSupported();
    }

    public bool ApplyGateway(RdpGatewayConfiguration gateway)
    {
        _ = gateway;
        return false;
    }

    public void ApplyConfiguration(RdpRuntimeConfiguration configuration)
    {
        _ = configuration;
        ThrowNotSupported();
    }

    public void ApplyDisplay(RdpDisplayConfiguration display)
    {
        _ = display;
        ThrowNotSupported();
    }

    public bool TryUpdateDisplay(RdpDisplayConfiguration display)
    {
        _ = display;
        return false;
    }

    public string GetErrorDescription(int disconnectReason)
    {
        _ = disconnectReason;
        return "RDP ActiveX is unavailable on ARM64.";
    }
    public void SubscribeEvents()
    {
    }

    public void UnsubscribeEvents()
    {
    }

    public void Focus()
    {
    }

    public void Dispose()
    {
    }

    private static void ThrowNotSupported() =>
        throw new PlatformNotSupportedException("Microsoft Terminal Services ActiveX is not available on ARM64.");
}
