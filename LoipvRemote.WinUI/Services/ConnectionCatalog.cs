using LoipvRemote.Application.Configuration;
using LoipvRemote.Domain.Connections;

namespace LoipvRemote.WinUI.Services;

/// <summary>
/// Loads the UI-independent connection definition tree for the WinUI shell.
/// </summary>
public sealed class ConnectionCatalog
{
    private readonly ConnectionStoreConfigurationService _storeConfiguration;
    private readonly ConnectionStoreSettingsRepository _settingsRepository;
    private ConnectionStoreSettings? _settings;

    public ConnectionCatalog(
        ConnectionStoreConfigurationService storeConfiguration,
        ConnectionStoreSettingsRepository settingsRepository)
    {
        _storeConfiguration = storeConfiguration ?? throw new ArgumentNullException(nameof(storeConfiguration));
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
    }

    public async Task<ConnectionCatalogLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        var tree = await LoadTreeAsync(settings, cancellationToken).ConfigureAwait(false);
        _settings = settings;
        SetTree(tree);
        return new ConnectionCatalogLoadResult(
            _tree,
            _connections,
            settings.Kind == ConnectionDefinitionStoreKind.Xml && !File.Exists(settings.Location)
                ? "No connection file has been created yet."
                : $"{tree.Connections.Count} connections loaded from {settings.Kind}.");
    }

    public ConnectionDefinition? FindConnection(Guid id) =>
        _connections.TryGetValue(id, out ConnectionDefinition? connection) ? connection : null;

    public ConnectionDefinition? FindResolvedConnection(Guid id) =>
        _connections.ContainsKey(id) ? ConnectionOptionInheritanceResolver.Resolve(_tree, id) : null;

    public ConnectionTreeDefinition Tree => _tree;

    public ConnectionStoreSettings Settings => _settings ?? throw new InvalidOperationException("Load the connection catalog before accessing its settings.");

    public bool IsLoaded => _settings is not null;

    public async Task SaveAsync(ConnectionTreeDefinition tree, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tree);
        tree.Validate();
        ConnectionStoreSettings settings = Settings;
        if (settings.IsReadOnly)
            throw new InvalidOperationException("The selected connection store is read-only.");

        await _storeConfiguration.SaveAsync(settings.ToOptions(), tree, cancellationToken).ConfigureAwait(false);
        SetTree(tree);
    }

    /// <summary>
    /// Selects a new explicit store without touching its contents. The former
    /// store remains unchanged, so a user must explicitly choose migration to
    /// overwrite a target store.
    /// </summary>
    public async Task<ConnectionCatalogLoadResult> ChangeStoreAsync(
        ConnectionStoreSettings settings,
        bool migrateCurrentTree,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        if (migrateCurrentTree)
        {
            if (settings.IsReadOnly)
                throw new InvalidOperationException("A read-only connection store cannot receive a migration.");
            await _storeConfiguration.SaveAsync(settings.ToOptions(), _tree, cancellationToken).ConfigureAwait(false);
        }

        var tree = await LoadTreeAsync(settings, cancellationToken).ConfigureAwait(false);
        await _settingsRepository.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        _settings = settings;
        SetTree(tree);
        return new ConnectionCatalogLoadResult(_tree, _connections, $"{tree.Connections.Count} connections loaded from {settings.Kind}.");
    }

    private Task<ConnectionTreeDefinition> LoadTreeAsync(ConnectionStoreSettings settings, CancellationToken cancellationToken) =>
        settings.Kind == ConnectionDefinitionStoreKind.Xml && !File.Exists(settings.Location)
            ? Task.FromResult(ConnectionTreeDefinition.Empty)
            : _storeConfiguration.LoadAsync(settings.ToOptions(), cancellationToken);

    private void SetTree(ConnectionTreeDefinition tree)
    {
        _tree = tree;
        _connections = tree.Connections.ToDictionary(connection => connection.Id);
    }

    private ConnectionTreeDefinition _tree = ConnectionTreeDefinition.Empty;
    private Dictionary<Guid, ConnectionDefinition> _connections = [];
}

public sealed record ConnectionCatalogLoadResult(
    ConnectionTreeDefinition Tree,
    IReadOnlyDictionary<Guid, ConnectionDefinition> Connections,
    string Message);
