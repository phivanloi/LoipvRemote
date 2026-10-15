using System.Data.Common;
using Npgsql;
using LoipvRemote.Infrastructure.Persistence.Relational;

namespace LoipvRemote.Infrastructure.Persistence.PostgreSql;

/// <summary>PostgreSQL persistence for connection definitions.</summary>
public sealed class PostgreSqlConnectionDefinitionStore(string connectionString) : RelationalConnectionDefinitionStore(connectionString)
{
    protected override DbConnection CreateConnection(string connectionString) => new NpgsqlConnection(connectionString);

    protected override string CreateSchemaVersionTableSql => "CREATE TABLE IF NOT EXISTS connection_schema_version (id INTEGER NOT NULL PRIMARY KEY, version INTEGER NOT NULL)";

    protected override string CreateTableSql => "CREATE TABLE IF NOT EXISTS connection_definitions (id VARCHAR(36) NOT NULL PRIMARY KEY, name VARCHAR(512) NOT NULL, host VARCHAR(2048) NOT NULL, port INTEGER NOT NULL, protocol VARCHAR(64) NOT NULL, credential_provider VARCHAR(128) NOT NULL, credential_identifier VARCHAR(2048) NOT NULL, parent_folder_id VARCHAR(36) NULL, sort_order INTEGER NOT NULL DEFAULT 0, options_json TEXT NULL, gateway_credential_provider VARCHAR(128) NULL, gateway_credential_identifier VARCHAR(2048) NULL)";

    protected override string CreateFoldersTableSql => "CREATE TABLE IF NOT EXISTS connection_folders (id VARCHAR(36) NOT NULL PRIMARY KEY, name VARCHAR(512) NOT NULL, parent_folder_id VARCHAR(36) NULL, sort_order INTEGER NOT NULL, options_json TEXT NULL, is_root INTEGER NOT NULL DEFAULT 0)";
}
