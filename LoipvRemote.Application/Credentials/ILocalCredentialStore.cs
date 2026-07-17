using LoipvRemote.Domain.Credentials;

namespace LoipvRemote.Application.Credentials;

/// <summary>Owns named current-user credentials whose secrets never enter a connection store.</summary>
public interface ILocalCredentialStore
{
    Task<IReadOnlyList<LocalCredentialDefinition>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LocalCredentialDefinition credential, string password, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid credentialId, CancellationToken cancellationToken = default);

    string? ResolvePassword(CredentialReference reference);
}

public sealed record LocalCredentialDefinition(Guid Id, string Name, string UserName)
{
    public void Validate()
    {
        if (Id == Guid.Empty)
            throw new ArgumentException("Credential ID is required.", nameof(Id));
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Credential name is required.", nameof(Name));
    }
}
