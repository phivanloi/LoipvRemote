using LoipvRemote.Domain.Protocols;

namespace LoipvRemote.Protocols.Vnc;

/// <summary>Protocol lifecycle independent of any desktop UI framework.</summary>
public sealed class VncSession
{
    private static readonly TimeSpan EndpointProbeTimeout = TimeSpan.FromMilliseconds(500);
    private readonly IVncClient _client;
    private readonly IVncEndpointProbe _endpointProbe;
    private VncConnectionOptions? _options;

    public VncSession(IVncClient client, IVncEndpointProbe endpointProbe)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _endpointProbe = endpointProbe ?? throw new ArgumentNullException(nameof(endpointProbe));
    }

    public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;

    public bool Initialize(VncConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (State is not ProtocolSessionState.Created and not ProtocolSessionState.Closed)
            return false;

        _client.SetPort(options.Port);
        _options = options;
        State = ProtocolSessionState.Initialized;
        return true;
    }

    public bool Connect(CancellationToken cancellationToken = default)
    {
        if (State != ProtocolSessionState.Initialized || _options is null)
            return false;

        _endpointProbe.ProbeAsync(_options.Host, _options.Port, EndpointProbeTimeout, cancellationToken)
            .GetAwaiter()
            .GetResult();
        _client.Connect(_options.Host, _options.ViewOnly, _options.SmartSize);
        State = ProtocolSessionState.Connected;
        return true;
    }

    public async ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (State != ProtocolSessionState.Initialized || _options is null)
            return false;

        await _endpointProbe.ProbeAsync(_options.Host, _options.Port, EndpointProbeTimeout, cancellationToken).ConfigureAwait(false);
        if (_client is IAsyncVncClient asyncClient)
            await asyncClient.ConnectAsync(_options.Host, _options.ViewOnly, _options.SmartSize, cancellationToken).ConfigureAwait(false);
        else
            _client.Connect(_options.Host, _options.ViewOnly, _options.SmartSize);
        State = ProtocolSessionState.Connected;
        return true;
    }

    public void Disconnect()
    {
        if (State is ProtocolSessionState.Closed or ProtocolSessionState.Created)
            return;

        if (_client is IAsyncVncClient asyncClient)
            asyncClient.DisconnectAsync().AsTask().GetAwaiter().GetResult();
        else
            _client.Disconnect();
        State = ProtocolSessionState.Closed;
    }
}
