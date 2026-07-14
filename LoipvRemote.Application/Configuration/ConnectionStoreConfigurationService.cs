using LoipvRemote.Domain.Connections;

namespace LoipvRemote.UseCases.Configuration;

/// <summary>Configuration use case for explicitly selected connection definition stores.</summary>
public sealed class ConnectionStoreConfigurationService(IConnectionDefinitionStoreFactory storeFactory)
{
    private readonly IConnectionDefinitionStoreFactory _storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));

    public Task<ConnectionTreeDefinition> LoadAsync(
        ConnectionDefinitionStoreOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return _storeFactory.Create(options).LoadAsync(cancellationToken);
    }

    public Task SaveAsync(
        ConnectionDefinitionStoreOptions options,
        ConnectionTreeDefinition tree,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tree);
        tree.Validate();

        return _storeFactory.Create(options).SaveAsync(tree, cancellationToken);
    }
}
