using LoipvRemote.Infrastructure.Persistence.MySql;
using LoipvRemote.Infrastructure.Persistence.Odbc;
using LoipvRemote.Infrastructure.Persistence.Sqlite;
using LoipvRemote.Infrastructure.Persistence.SqlServer;
using LoipvRemote.Infrastructure.Persistence.Xml;
using LoipvRemote.UseCases.Configuration;

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
            ConnectionDefinitionStoreKind.Sqlite => new SqliteConnectionDefinitionStore(options.Location),
            ConnectionDefinitionStoreKind.SqlServer => new SqlServerConnectionDefinitionStore(options.Location),
            ConnectionDefinitionStoreKind.MySql => new MySqlConnectionDefinitionStore(options.Location),
            ConnectionDefinitionStoreKind.Odbc => new OdbcConnectionDefinitionStore(options.Location),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Kind, "Unsupported connection definition store.")
        };
    }
}
