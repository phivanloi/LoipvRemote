using LoipvRemote.Connectors.Abstractions;

namespace LoipvRemote.Connectors.OnePassword;

public sealed class OnePasswordCredentialConnector : IExternalCredentialConnector
{
    public string Provider => "OnePassword";

    public Task<ExternalCredential> ResolveAsync(
        string secretReference,
        CancellationToken cancellationToken = default)
    {
        return OnePasswordCli.ReadPasswordAsync(secretReference, cancellationToken);
    }
}
