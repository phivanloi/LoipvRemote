using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Connectors.Passwordstate;

public sealed class PasswordstateCredentialConnector : IExternalCredentialConnector
{
    private readonly PasswordstateInterface _client;

    public PasswordstateCredentialConnector(
        IExternalCredentialPrompt prompt,
        IExternalCredentialSettingsStore settings)
    {
        _client = new PasswordstateInterface(prompt, settings);
    }

    public string Provider => "ClickstudiosPasswordState";

    public Task<ExternalCredential> ResolveAsync(
        string secretReference,
        CancellationToken cancellationToken = default)
    {
        return _client.FetchSecretFromServerAsync(secretReference, cancellationToken);
    }
}
