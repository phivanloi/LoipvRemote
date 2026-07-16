namespace LoipvRemote.Connectors.Abstractions;

public sealed class ExternalCredentialConnectorRegistry(IEnumerable<IExternalCredentialConnector> connectors)
{
    private readonly Dictionary<string, IExternalCredentialConnector> _connectors =
        connectors?.ToDictionary(connector => connector.Provider, StringComparer.OrdinalIgnoreCase)
        ?? throw new ArgumentNullException(nameof(connectors));

    public IExternalCredentialConnector GetRequired(string provider) =>
        !string.IsNullOrWhiteSpace(provider) && _connectors.TryGetValue(provider, out IExternalCredentialConnector? connector)
            ? connector
            : throw new NotSupportedException($"Credential provider '{provider}' is not registered.");

    public Task<ExternalCredential> ResolveAsync(
        string provider,
        ExternalCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IExternalCredentialConnector connector = GetRequired(provider);
        return connector is IContextualExternalCredentialConnector contextual
            ? contextual.ResolveAsync(request, cancellationToken)
            : connector.ResolveAsync(request.SecretReference, cancellationToken);
    }
}
