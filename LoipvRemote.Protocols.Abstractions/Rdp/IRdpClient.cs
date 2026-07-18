using LoipvRemote.Domain.Protocols.Rdp;

namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Minimal RDP transport surface required by the protocol lifecycle.</summary>
public interface IRdpClient
{
    void Initialize();
    void ConfigureEndpoint(string host, int port);
    void Connect();
    void Disconnect();
}

/// <summary>Creates a platform-specific RDP client for a requested protocol generation.</summary>
public interface IRdpClientFactory
{
    IRdpClient Create(RdpVersion requestedVersion);
}

/// <summary>Optional RDP client capability for applying runtime credentials and gateway settings.</summary>
public interface IRdpCredentialClient
{
    void ApplyCredentials(RdpCredentialConfiguration credentials);

    bool ApplyGateway(RdpGatewayConfiguration gateway);
}

/// <summary>Optional advanced runtime settings supported by the Windows ActiveX client.</summary>
public interface IRdpRuntimeClient
{
    void ApplyConfiguration(RdpRuntimeConfiguration configuration);
    void ApplyDisplay(RdpDisplayConfiguration display);
}

/// <summary>
/// Optional capability for changing the remote desktop size without
/// reconnecting an already established RDP session.
/// </summary>
public interface IRdpDynamicDisplayClient
{
    /// <returns><see langword="true"/> when the ActiveX control accepted the update.</returns>
    bool TryUpdateDisplay(RdpDisplayConfiguration display);
}

/// <summary>Asynchronous events emitted by the Windows RDP ActiveX control.</summary>
public interface IRdpEventClient
{
    event EventHandler? Connecting;
    event EventHandler? Connected;
    event EventHandler? LoginComplete;
    event EventHandler<int>? FatalError;
    event EventHandler<int>? Disconnected;
    event EventHandler? IdleTimeout;
    event EventHandler? LeaveFullScreen;

    string GetErrorDescription(int disconnectReason);
    void SubscribeEvents();
    void UnsubscribeEvents();
}
