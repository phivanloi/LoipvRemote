using LoipvRemote.Application.Configuration;
using LoipvRemote.Application.Credentials;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;

namespace LoipvRemote.WinUI.Services;

/// <summary>
/// Converts the plaintext credential payload from a user-selected export into
/// DPAPI-protected options for the current Windows user.
/// </summary>
public sealed class PortableConnectionCredentialImporter(IStringSecretStore secretStore)
{
    private const string ProtectedSecretPrefix = "$dpapi-secret:";
    private readonly IStringSecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));

    public ConnectionTreeDefinition Apply(
        ConnectionTreeDefinition tree,
        IReadOnlyDictionary<Guid, Guid> connectionIds,
        IReadOnlyDictionary<Guid, PortableConnectionCredential> credentials)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(connectionIds);
        ArgumentNullException.ThrowIfNull(credentials);
        Dictionary<Guid, Guid> sourceIdsByImportedId = connectionIds.ToDictionary(pair => pair.Value, pair => pair.Key);

        ConnectionDefinition[] connections = tree.Connections.Select(connection =>
        {
            if (!sourceIdsByImportedId.TryGetValue(connection.Id, out Guid importedSourceId))
                return connection;

            credentials.TryGetValue(importedSourceId, out PortableConnectionCredential? portable);
            Dictionary<string, string> values = connection.Options is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : connection.Options.Values
                    .Where(pair => !pair.Key.StartsWith(ProtectedSecretPrefix, StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

            if (portable is { UserName.Length: > 0 })
                values["Username"] = portable.UserName;
            if (portable is { Password.Length: > 0 })
                values[ProtectedSecretPrefix + "Password"] = Protect(connection.Id, "Password", portable.Password);
            if (portable is { GatewayPassword.Length: > 0 })
                values[ProtectedSecretPrefix + "RDGatewayPassword"] = Protect(connection.Id, "RDGatewayPassword", portable.GatewayPassword);

            return connection with
            {
                Credential = CredentialReference.None,
                GatewayCredential = null,
                Options = values.Count == 0 && connection.Options?.InheritedProperties.Count is not > 0
                    ? null
                    : new ConnectionNodeOptions(values, connection.Options?.InheritedProperties.ToArray() ?? [])
            };
        }).ToArray();

        var updated = tree with { Connections = connections };
        updated.Validate();
        return updated;
    }

    private string Protect(Guid connectionId, string propertyName, string password) =>
        _secretStore.Protect(
            password,
            ConnectionSecretPurposes.ForConnectionOption(connectionId.ToString("D"), propertyName));
}
