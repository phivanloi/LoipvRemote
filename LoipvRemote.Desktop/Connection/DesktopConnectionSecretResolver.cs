using LoipvRemote.Domain.Connections;
using LoipvRemote.UseCases.Credentials;

namespace LoipvRemote.Connection;

/// <summary>Desktop adapter that unwraps DPAPI-protected connection options on demand.</summary>
public sealed class DesktopConnectionSecretResolver(IStringSecretStore secretStore) : IConnectionSecretResolver
{
    private const string ProtectedSecretOptionPrefix = "$dpapi-secret:";
    private readonly IStringSecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));

    public string? Resolve(ConnectionDefinition definition, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        string key = ProtectedSecretOptionPrefix + propertyName;
        if (definition.Options?.Values.TryGetValue(key, out string? protectedValue) != true ||
            string.IsNullOrWhiteSpace(protectedValue))
            return null;

        return _secretStore.Unprotect(
            protectedValue,
            ConnectionSecretPurposes.ForConnectionOption(definition.Id.ToString(), propertyName));
    }
}
