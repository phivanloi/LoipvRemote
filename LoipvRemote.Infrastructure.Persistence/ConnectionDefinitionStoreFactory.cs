using LoipvRemote.Infrastructure.Persistence.PostgreSql;
using LoipvRemote.Infrastructure.Persistence.Xml;
using LoipvRemote.Application.Configuration;

namespace LoipvRemote.Infrastructure.Persistence;

/// <summary>Infrastructure implementation for selecting a configured persistence provider.</summary>
public sealed class ConnectionDefinitionStoreFactory : IConnectionDefinitionStoreFactory
{
    public IConnectionDefinitionStore Create(ConnectionDefinitionStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Location);

        return options.Kind switch
        {
            ConnectionDefinitionStoreKind.Xml => new XmlConnectionDefinitionStore(options.Location),
            ConnectionDefinitionStoreKind.PostgreSql => new PostgreSqlConnectionDefinitionStore(options.Location),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Kind, "Unsupported connection definition store.")
        };
    }
}
