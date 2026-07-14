namespace LoipvRemote.Connectors.Abstractions;

public enum ExternalCredentialProtocol
{
    Rdp,
    Ssh
}

public sealed record ExternalCredentialRequest(
    string SecretReference,
    string Username,
    string Host,
    string Mount,
    string Role,
    int SecretEngine,
    ExternalCredentialProtocol Protocol);
