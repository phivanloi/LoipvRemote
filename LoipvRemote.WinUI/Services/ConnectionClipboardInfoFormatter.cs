using LoipvRemote.Application.Credentials;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;

namespace LoipvRemote.WinUI.Services;

/// <summary>Builds the user-requested plaintext connection summary for the Windows clipboard.</summary>
public static class ConnectionClipboardInfoFormatter
{
    public static string Format(
        ConnectionDefinition definition,
        IReadOnlyCollection<LocalCredentialDefinition> localCredentials,
        string? password)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(localCredentials);

        string userName = ResolveUserName(definition, localCredentials);
        return $"{definition.Host.Trim()}:{definition.Port} | {userName} / {password ?? string.Empty}";
    }

    private static string ResolveUserName(
        ConnectionDefinition definition,
        IReadOnlyCollection<LocalCredentialDefinition> localCredentials)
    {
        if (definition.Options?.Values.TryGetValue("Username", out string? configuredUserName) == true &&
            !string.IsNullOrWhiteSpace(configuredUserName))
        {
            return configuredUserName.Trim();
        }

        if (!string.Equals(
                definition.Credential.Provider,
                CredentialReference.LocalDpapiProvider,
                StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(definition.Credential.Identifier, out Guid credentialId))
        {
            return string.Empty;
        }

        return localCredentials
                   .SingleOrDefault(credential => credential.Id == credentialId)
                   ?.UserName
                   .Trim() ?? string.Empty;
    }
}
