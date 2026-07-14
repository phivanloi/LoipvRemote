using LoipvRemote.Domain.Protocols;

namespace LoipvRemote.Protocols.Vnc;

/// <summary>Protocol lifecycle independent of the WinForms VNC control.</summary>
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

    public void Disconnect()
    {
        if (State is ProtocolSessionState.Closed or ProtocolSessionState.Created)
            return;

        _client.Disconnect();
        State = ProtocolSessionState.Closed;
    }
}
