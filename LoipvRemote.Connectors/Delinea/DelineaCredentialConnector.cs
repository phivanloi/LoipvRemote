using LoipvRemote.Connectors.Abstractions;

namespace LoipvRemote.Connectors.Delinea;

public sealed class DelineaCredentialConnector : IExternalCredentialConnector
{
    public string Provider => "DelineaSecretServer";

    public ExternalCredential Resolve(string secretReference)
    {
        SecretServerInterface.FetchSecretFromServer(secretReference, out string username, out string password, out string domain, out string privateKey);
        return new ExternalCredential(username, password, domain, privateKey);
    }
}
