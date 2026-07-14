namespace LoipvRemote.Protocols.Browser;

/// <summary>Builds browser protocol endpoints without string-based port concatenation.</summary>
public static class BrowserEndpointUriBuilder
{
    public static Uri Build(string host, int port, string defaultScheme, int defaultPort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultScheme);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defaultPort);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(defaultPort, 65535);

        var endpoint = Uri.TryCreate(host, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri($"{defaultScheme}://{host}", UriKind.Absolute);

        var builder = new UriBuilder(endpoint);
        if (port != defaultPort)
            builder.Port = port;

        return builder.Uri;
    }
}
