using System.Data.Common;
using MySql.Data.MySqlClient;
using LoipvRemote.Infrastructure.Persistence.Relational;

namespace LoipvRemote.Infrastructure.Persistence.MySql;

public sealed class MySqlConnectionDefinitionStore(string connectionString) : RelationalConnectionDefinitionStore(connectionString)
{
    protected override DbConnection CreateConnection(string connectionString) => new MySqlConnection(connectionString);
    protected override string CreateSchemaVersionTableSql => "CREATE TABLE IF NOT EXISTS connection_schema_version (id INT NOT NULL PRIMARY KEY, version INT NOT NULL)";
    protected override string CreateTableSql => "CREATE TABLE IF NOT EXISTS connection_definitions (id VARCHAR(36) NOT NULL PRIMARY KEY, name VARCHAR(512) NOT NULL, host VARCHAR(2048) NOT NULL, port INT NOT NULL, protocol VARCHAR(64) NOT NULL, credential_provider VARCHAR(128) NOT NULL, credential_identifier VARCHAR(2048) NOT NULL, external_display_name VARCHAR(512) NULL, external_executable_path VARCHAR(2048) NULL, external_arguments TEXT NULL, external_working_directory VARCHAR(2048) NULL, external_run_elevated INT NULL, external_embed_window INT NULL, external_wait_for_exit INT NULL, parent_folder_id VARCHAR(36) NULL, sort_order INT NOT NULL DEFAULT 0, options_json TEXT NULL, gateway_credential_provider VARCHAR(128) NULL, gateway_credential_identifier VARCHAR(2048) NULL)";
    protected override string CreateFoldersTableSql => "CREATE TABLE IF NOT EXISTS connection_folders (id VARCHAR(36) NOT NULL PRIMARY KEY, name VARCHAR(512) NOT NULL, parent_folder_id VARCHAR(36) NULL, sort_order INT NOT NULL, options_json TEXT NULL, is_root INT NOT NULL DEFAULT 0)";
}
