namespace LoipvRemote.Connectors.Abstractions;

public sealed class ExternalCredentialConnectorRegistry(IEnumerable<IExternalCredentialConnector> connectors)
{
    private readonly IReadOnlyDictionary<string, IExternalCredentialConnector> _connectors =
        connectors?.ToDictionary(connector => connector.Provider, StringComparer.OrdinalIgnoreCase)
        ?? throw new ArgumentNullException(nameof(connectors));

    public IExternalCredentialConnector GetRequired(string provider) =>
        !string.IsNullOrWhiteSpace(provider) && _connectors.TryGetValue(provider, out IExternalCredentialConnector? connector)
            ? connector
            : throw new NotSupportedException($"Credential provider '{provider}' is not registered.");

    public ExternalCredential Resolve(string provider, ExternalCredentialRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        IExternalCredentialConnector connector = GetRequired(provider);
        return connector is IContextualExternalCredentialConnector contextual
            ? contextual.Resolve(request)
            : connector.Resolve(request.SecretReference);
    }
}
