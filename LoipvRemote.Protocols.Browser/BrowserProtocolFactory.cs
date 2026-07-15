using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Browser;

/// <summary>Creates browser sessions from Domain connection definitions.</summary>
public sealed class BrowserProtocolFactory(
    Func<IBrowserClient> clientFactory,
    Func<IEmbeddedWindowOperations>? windowOperationsFactory = null) : IProtocolFactory
{
    private readonly Func<IBrowserClient> _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    private readonly Func<IEmbeddedWindowOperations>? _windowOperationsFactory = windowOperationsFactory;

    public IProtocolSession Create(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        (string scheme, int defaultPort) = definition.Protocol switch
        {
            ProtocolKind.Http => ("http", 80),
            ProtocolKind.Https => ("https", 443),
            ProtocolKind.Browser => ResolveBrowserScheme(definition.Options),
            _ => throw new NotSupportedException($"Protocol '{definition.Protocol}' is not handled by {nameof(BrowserProtocolFactory)}.")
        };

        var options = new BrowserConnectionOptions(definition.Host, definition.Port, scheme, defaultPort);
        return new BrowserProtocolSession(_clientFactory(), options, _windowOperationsFactory?.Invoke());
    }

    private static (string Scheme, int DefaultPort) ResolveBrowserScheme(ConnectionNodeOptions? options)
    {
        if (options?.Values.TryGetValue("Scheme", out string? configuredScheme) == true &&
            !string.IsNullOrWhiteSpace(configuredScheme))
        {
            string scheme = configuredScheme.Trim().ToLowerInvariant();
            return scheme switch
            {
                "http" => (scheme, 80),
                "https" => (scheme, 443),
                _ => throw new ArgumentException("Browser Scheme must be 'http' or 'https'.", nameof(options))
            };
        }

        return ("https", 443);
    }
}
