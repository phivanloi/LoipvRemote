using LoipvRemote.Domain.Connections;

namespace LoipvRemote.UseCases.Configuration;

/// <summary>Persistence port for secret-free, ordered connection trees.</summary>
public interface IConnectionDefinitionStore
{
    Task<ConnectionTreeDefinition> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ConnectionTreeDefinition tree, CancellationToken cancellationToken = default);
}
