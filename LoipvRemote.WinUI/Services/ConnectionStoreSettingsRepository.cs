using System.Text.Json;
using LoipvRemote.Application.Configuration;
using LoipvRemote.Application.Credentials;

namespace LoipvRemote.WinUI.Services;

/// <summary>
/// Persists the WinUI connection-store selection separately from connection data.
/// Store locations are DPAPI-protected because a database connection string can
/// contain credentials.
/// </summary>
public sealed class ConnectionStoreSettingsRepository
{
    private const string StoreLocationPurpose = "WinUi.ConnectionStore.Location";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IStringSecretStore _secretStore;
    private readonly string _settingsFile;

    public ConnectionStoreSettingsRepository(IStringSecretStore secretStore)
        : this(secretStore, DefaultSettingsFile)
    {
    }

    internal ConnectionStoreSettingsRepository(IStringSecretStore secretStore, string settingsFile)
    {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFile);
        _settingsFile = settingsFile;
    }

    public async Task<ConnectionStoreSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFile))
            return ConnectionStoreSettings.CreateDefault();

        await using FileStream stream = File.OpenRead(_settingsFile);
        PersistedConnectionStoreSettings? persisted = await JsonSerializer.DeserializeAsync<PersistedConnectionStoreSettings>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (persisted is null || string.IsNullOrWhiteSpace(persisted.Kind) || string.IsNullOrWhiteSpace(persisted.ProtectedLocation))
            throw new InvalidDataException("The WinUI connection-store settings file is incomplete.");
        if (!Enum.TryParse(persisted.Kind, ignoreCase: true, out ConnectionDefinitionStoreKind kind))
            throw new InvalidDataException($"The configured connection-store kind '{persisted.Kind}' is invalid.");

        string location = _secretStore.Unprotect(persisted.ProtectedLocation, StoreLocationPurpose);
        AppThemeMode theme = Enum.TryParse(persisted.Theme, ignoreCase: true, out AppThemeMode parsedTheme)
            ? parsedTheme
            : AppThemeMode.System;
        return new ConnectionStoreSettings(kind, location, persisted.IsReadOnly, theme);
    }

    public async Task SaveAsync(ConnectionStoreSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        string? directory = Path.GetDirectoryName(_settingsFile);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var persisted = new PersistedConnectionStoreSettings(
            settings.Kind.ToString(),
            _secretStore.Protect(settings.Location, StoreLocationPurpose),
            settings.IsReadOnly,
            settings.Theme.ToString());
        string temporaryFile = _settingsFile + ".tmp";
        try
        {
            await using (FileStream stream = File.Create(temporaryFile))
                await JsonSerializer.SerializeAsync(stream, persisted, JsonOptions, cancellationToken).ConfigureAwait(false);

            File.Move(temporaryFile, _settingsFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
                File.Delete(temporaryFile);
        }
    }

    private static string DefaultSettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LoipvRemote",
        "winui-settings.json");

    private sealed record PersistedConnectionStoreSettings(string Kind, string ProtectedLocation, bool IsReadOnly, string? Theme = null);
}

public sealed record ConnectionStoreSettings(
    ConnectionDefinitionStoreKind Kind,
    string Location,
    bool IsReadOnly = false,
    AppThemeMode Theme = AppThemeMode.System)
{
    public static ConnectionStoreSettings CreateDefault() => new(
        ConnectionDefinitionStoreKind.Xml,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LoipvRemote", "confCons.xml"));

    public ConnectionDefinitionStoreOptions ToOptions() => new(Kind, Location);

    public void Validate()
    {
        if (!Enum.IsDefined(Kind))
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unsupported connection-store kind.");
        if (!Enum.IsDefined(Theme))
            throw new ArgumentOutOfRangeException(nameof(Theme), Theme, "Unsupported application theme.");
        ArgumentException.ThrowIfNullOrWhiteSpace(Location);
    }
}

public enum AppThemeMode
{
    System,
    Light,
    Dark
}
