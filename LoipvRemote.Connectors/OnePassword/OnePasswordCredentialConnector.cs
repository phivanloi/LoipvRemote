using LoipvRemote.Connectors.Abstractions;

namespace LoipvRemote.Connectors.OnePassword;

public sealed class OnePasswordCredentialConnector : IExternalCredentialConnector
{
    public string Provider => "OnePassword";

    public ExternalCredential Resolve(string secretReference)
    {
        OnePasswordCli.ReadPassword(secretReference, out string username, out string password, out string domain, out string privateKey);
        return new ExternalCredential(username, password, domain, privateKey);
    }
}
