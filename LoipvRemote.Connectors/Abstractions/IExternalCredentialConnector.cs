namespace LoipvRemote.Connectors.Abstractions;

public interface IExternalCredentialConnector
{
    string Provider { get; }

    Task<ExternalCredential> ResolveAsync(
        string secretReference,
        CancellationToken cancellationToken = default);
}

public interface IContextualExternalCredentialConnector : IExternalCredentialConnector
{
    Task<ExternalCredential> ResolveAsync(
        ExternalCredentialRequest request,
        CancellationToken cancellationToken = default);
}
