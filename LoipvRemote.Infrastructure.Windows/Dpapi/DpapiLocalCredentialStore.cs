using System.Text.Json;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Application.Credentials;

namespace LoipvRemote.Infrastructure.Windows.Dpapi;

/// <summary>Persists named credential metadata plus DPAPI-protected secrets for the current user.</summary>
public sealed class DpapiLocalCredentialStore : ILocalCredentialStore
{
    private const string PasswordPurposePrefix = "LoipvRemote.LocalCredential.Password:";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IStringSecretStore _secretStore;
    private readonly string _filePath;
    private readonly object _gate = new();
    private List<PersistedCredential>? _credentials;

    public DpapiLocalCredentialStore(IStringSecretStore secretStore)
        : this(secretStore, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LoipvRemote", "credentials.json"))
    {
    }

    internal DpapiLocalCredentialStore(IStringSecretStore secretStore, string filePath)
    {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<IReadOnlyList<LocalCredentialDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        List<PersistedCredential> credentials = await GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
        return credentials.Select(item => new LocalCredentialDefinition(item.Id, item.Name, item.UserName)).ToArray();
    }

    public async Task SaveAsync(LocalCredentialDefinition credential, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        credential.Validate();
        ArgumentException.ThrowIfNullOrEmpty(password);
        List<PersistedCredential> credentials = await GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
        string protectedPassword = _secretStore.Protect(password, PasswordPurpose(credential.Id));
        var persisted = new PersistedCredential(credential.Id, credential.Name.Trim(), credential.UserName.Trim(), protectedPassword);
        int index = credentials.FindIndex(item => item.Id == credential.Id);
        if (index >= 0)
            credentials[index] = persisted;
        else
            credentials.Add(persisted);
        await PersistAsync(credentials, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid credentialId, CancellationToken cancellationToken = default)
    {
        if (credentialId == Guid.Empty)
            throw new ArgumentException("Credential ID is required.", nameof(credentialId));
        List<PersistedCredential> credentials = await GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
        credentials.RemoveAll(item => item.Id == credentialId);
        await PersistAsync(credentials, cancellationToken).ConfigureAwait(false);
    }

    public string? ResolvePassword(CredentialReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!string.Equals(reference.Provider, CredentialReference.LocalDpapiProvider, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(reference.Identifier, out Guid credentialId))
        {
            return null;
        }

        lock (_gate)
        {
            PersistedCredential? credential = _credentials?.SingleOrDefault(item => item.Id == credentialId);
            return credential is null ? null : _secretStore.Unprotect(credential.ProtectedPassword, PasswordPurpose(credential.Id));
        }
    }

    private async Task<List<PersistedCredential>> GetCredentialsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_credentials is not null)
                return _credentials;
        }

        List<PersistedCredential> loaded = [];
        if (File.Exists(_filePath))
        {
            await using FileStream stream = File.OpenRead(_filePath);
            loaded = await JsonSerializer.DeserializeAsync<List<PersistedCredential>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The local credential store is invalid.");
        }
        lock (_gate)
            return _credentials ??= loaded;
    }

    private async Task PersistAsync(List<PersistedCredential> credentials, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        string temporaryPath = _filePath + ".tmp";
        try
        {
            await using (FileStream stream = File.Create(temporaryPath))
                await JsonSerializer.SerializeAsync(stream, credentials, JsonOptions, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string PasswordPurpose(Guid credentialId) => PasswordPurposePrefix + credentialId.ToString("D");

    private sealed record PersistedCredential(Guid Id, string Name, string UserName, string ProtectedPassword);
}
