namespace LoipvRemote.Connectors.Abstractions;

public interface IExternalCredentialConnector
{
    string Provider { get; }

    ExternalCredential Resolve(string secretReference);
}

public interface IContextualExternalCredentialConnector : IExternalCredentialConnector
{
    ExternalCredential Resolve(ExternalCredentialRequest request);
}
