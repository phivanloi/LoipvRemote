using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Connectors.Delinea;

public sealed class DelineaCredentialConnector : IExternalCredentialConnector
{
    private readonly SecretServerInterface _client;

    public DelineaCredentialConnector(
        IExternalCredentialPrompt prompt,
        IExternalCredentialSettingsStore settings)
    {
        _client = new SecretServerInterface(prompt, settings);
    }

    public string Provider => "DelineaSecretServer";

    public Task<ExternalCredential> ResolveAsync(
        string secretReference,
        CancellationToken cancellationToken = default)
    {
        return _client.FetchSecretFromServerAsync(secretReference, cancellationToken);
    }
}
