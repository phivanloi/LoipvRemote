using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;

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

        State = ProtocolSessionState.Initialized;
        return true;
    }

    public bool Connect()
    {
        if (State != ProtocolSessionState.Initialized)
            return false;

        _client.Connect();
        State = ProtocolSessionState.Connected;
        return true;
    }

    public void Disconnect()
    {
        if (State is ProtocolSessionState.Created or ProtocolSessionState.Closed)
            return;

        _client.Disconnect();
        State = ProtocolSessionState.Closed;
    }
}
