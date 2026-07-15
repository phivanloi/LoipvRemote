using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;
using System.Runtime.InteropServices;

namespace LoipvRemote.Protocols.Rdp;

/// <summary>RDP connection lifecycle independent of the ActiveX control.</summary>
public sealed class RdpSession
{
    private readonly IRdpClient _client;

    public RdpSession(IRdpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;

    public bool Initialize(RdpConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (State is not ProtocolSessionState.Created and not ProtocolSessionState.Closed)
            return false;

        _client.Initialize();
        _client.ConfigureEndpoint(options.Host, options.Port);
        if (_client is IRdpRuntimeClient runtimeClient)
        {
            if (options.RuntimeConfiguration is not null)
                runtimeClient.ApplyConfiguration(options.RuntimeConfiguration);
            if (options.DisplayConfiguration is not null)
                runtimeClient.ApplyDisplay(options.DisplayConfiguration);
        }
        if (_client is IRdpCredentialClient credentialClient)
        {
            credentialClient.ApplyCredentials(new RdpCredentialConfiguration(
                options.Username,
                options.Password,
                options.Domain,
                !string.IsNullOrEmpty(options.Password)));
            if (options.Gateway is not null)
                credentialClient.ApplyGateway(options.Gateway);
        }
        State = ProtocolSessionState.Initialized;
        return true;
    }

    public bool Connect()
    {
        if (State != ProtocolSessionState.Initialized)
            return false;

        _client.Connect();
        if (_client is not IRdpEventClient)
            State = ProtocolSessionState.Connected;
        return true;
    }

    internal void MarkConnected() => State = ProtocolSessionState.Connected;

    internal void MarkFaulted() => State = ProtocolSessionState.Faulted;

    internal void MarkClosed() => State = ProtocolSessionState.Closed;

    public void Disconnect()
    {
        if (State is ProtocolSessionState.Created or ProtocolSessionState.Closed)
            return;

        try
        {
            _client.Disconnect();
        }
        catch (COMException)
        {
            // The ActiveX control can already be disconnected when its host
            // window is being destroyed. Closing must remain idempotent and
            // must not surface a COM failure through the WinForms close path.
        }
        finally
        {
            State = ProtocolSessionState.Closed;
        }
    }
}
