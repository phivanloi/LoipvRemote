using LoipvRemote.Domain.Protocols;

namespace LoipvRemote.Protocols.Browser;

/// <summary>Browser protocol lifecycle independent of WebBrowser and WebView2.</summary>
public sealed class BrowserSession
{
    private readonly IBrowserClient _client;
    private Uri? _endpoint;

    public BrowserSession(IBrowserClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;

    public bool Initialize(BrowserConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (State is not ProtocolSessionState.Created and not ProtocolSessionState.Closed)
            return false;

        _endpoint = BrowserEndpointUriBuilder.Build(options.Host, options.Port, options.Scheme, options.DefaultPort);
        State = ProtocolSessionState.Initialized;
        return true;
    }

    public bool Connect()
    {
        if (State != ProtocolSessionState.Initialized || _endpoint is null)
            return false;

        _client.Navigate(_endpoint);
        State = ProtocolSessionState.Connected;
        return true;
    }

    public void Disconnect()
    {
        if (State is ProtocolSessionState.Created or ProtocolSessionState.Closed)
            return;

        State = ProtocolSessionState.Closed;
    }
}
