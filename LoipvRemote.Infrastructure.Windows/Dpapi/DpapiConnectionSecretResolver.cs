using LoipvRemote.Domain.Connections;
using LoipvRemote.Application.Credentials;

namespace LoipvRemote.Infrastructure.Windows.Dpapi;

/// <summary>
/// Resolves only explicitly DPAPI-protected connection options.  Desktop shells
/// use this adapter instead of duplicating credential handling in UI code.
/// </summary>
public sealed class DpapiConnectionSecretResolver(IStringSecretStore secretStore, ILocalCredentialStore localCredentialStore) : IConnectionSecretResolver
{
    private const string ProtectedSecretOptionPrefix = "$dpapi-secret:";
    private readonly IStringSecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    private readonly ILocalCredentialStore _localCredentialStore = localCredentialStore ?? throw new ArgumentNullException(nameof(localCredentialStore));

    public string? Resolve(ConnectionDefinition definition, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (string.Equals(propertyName, "Password", StringComparison.OrdinalIgnoreCase))
        {
            string? sharedPassword = _localCredentialStore.ResolvePassword(definition.Credential);
            if (sharedPassword is not null)
                return sharedPassword;
        }

        string key = ProtectedSecretOptionPrefix + propertyName;
        if (definition.Options?.Values.TryGetValue(key, out string? protectedValue) != true ||
            string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        return _secretStore.Unprotect(
            protectedValue,
            ConnectionSecretPurposes.ForConnectionOption(definition.Id.ToString(), propertyName));
    }
}
