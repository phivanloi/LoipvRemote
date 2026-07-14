using LoipvRemote.Domain.Connections;

namespace LoipvRemote.UseCases.Configuration;

/// <summary>Application lifecycle for loading and saving a validated Domain connection tree.</summary>
public sealed class ConnectionDefinitionPersistenceRuntime(ConnectionStoreConfigurationService configurationService)
{
    private readonly ConnectionStoreConfigurationService _configurationService = configurationService
        ?? throw new ArgumentNullException(nameof(configurationService));

    public Task<ConnectionTreeDefinition> LoadAsync(
        ConnectionDefinitionStoreOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return _configurationService.LoadAsync(options, cancellationToken);
    }

    public Task SaveAsync(
        ConnectionDefinitionStoreOptions options,
        ConnectionTreeDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(definition);
        return _configurationService.SaveAsync(options, definition, cancellationToken);
    }
}
