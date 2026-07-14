namespace LoipvRemote.Domain.Credentials;

/// <summary>Identifies a credential without carrying a secret value.</summary>
public sealed record CredentialReference(string Provider, string Identifier)
{
    public static CredentialReference None { get; } = new("none", "");

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider))
            throw new ArgumentException("Credential provider is required.", nameof(Provider));
        if (string.Equals(Provider, None.Provider, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(Identifier))
                throw new ArgumentException("The none credential reference cannot have an identifier.", nameof(Identifier));
            return;
        }

        if (string.IsNullOrWhiteSpace(Identifier))
            throw new ArgumentException("Credential identifier is required for an external provider.", nameof(Identifier));
    }
}
