using LoipvRemote.Connectors.Abstractions;

namespace LoipvRemote.Connectors.Passwordstate;

public sealed class PasswordstateCredentialConnector : IExternalCredentialConnector
{
    public string Provider => "ClickstudiosPasswordState";

    public ExternalCredential Resolve(string secretReference)
    {
        PasswordstateInterface.FetchSecretFromServer(secretReference, out string username, out string password, out string domain, out string privateKey);
        return new ExternalCredential(username, password, domain, privateKey);
    }
}
