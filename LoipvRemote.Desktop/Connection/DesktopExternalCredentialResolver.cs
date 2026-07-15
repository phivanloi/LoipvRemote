using LoipvRemote.Connectors.Abstractions;

namespace LoipvRemote.Connection;

/// <summary>Resolves external credentials for a connection clone at session start.</summary>
public sealed class DesktopExternalCredentialResolver(ExternalCredentialConnectorRegistry registry)
{
    private readonly ExternalCredentialConnectorRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public ExternalCredential? Resolve(ConnectionInfo connection, bool gateway = false)
    {
        ArgumentNullException.ThrowIfNull(connection);

        ExternalCredentialProvider provider = gateway
            ? connection.RDGatewayExternalCredentialProvider
            : connection.ExternalCredentialProvider;
        string reference = gateway ? connection.RDGatewayUserViaAPI : connection.UserViaAPI;
        if (provider == ExternalCredentialProvider.None || string.IsNullOrWhiteSpace(reference))
            return null;

        ExternalCredentialProtocol protocol = connection.Protocol is ProtocolKind.Rdp
            ? ExternalCredentialProtocol.Rdp
            : ExternalCredentialProtocol.Ssh;
        ExternalCredentialRequest request = new(
            reference,
            gateway ? connection.RDGatewayUsername : connection.Username,
            connection.Hostname,
            connection.VaultOpenbaoMount,
            connection.VaultOpenbaoRole,
            (int)connection.VaultOpenbaoSecretEngine,
            protocol);

        return _registry.Resolve(provider.ToString(), request);
    }
}
