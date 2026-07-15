using System.Data.Common;
using System.Data.Odbc;
using LoipvRemote.Infrastructure.Persistence.Relational;

namespace LoipvRemote.Infrastructure.Persistence.Odbc;

public sealed class OdbcConnectionDefinitionStore(string connectionString) : RelationalConnectionDefinitionStore(connectionString)
{
    protected override DbConnection CreateConnection(string connectionString) => new OdbcConnection(connectionString);
    protected override bool IgnoreExistingTableError => true;
    protected override string CreateSchemaVersionTableSql => "CREATE TABLE connection_schema_version (id INTEGER NOT NULL PRIMARY KEY, version INTEGER NOT NULL)";
    protected override string CreateTableSql => "CREATE TABLE connection_definitions (id VARCHAR(36) NOT NULL PRIMARY KEY, name VARCHAR(512) NOT NULL, host VARCHAR(2048) NOT NULL, port INTEGER NOT NULL, protocol VARCHAR(64) NOT NULL, credential_provider VARCHAR(128) NOT NULL, credential_identifier VARCHAR(2048) NOT NULL, external_display_name VARCHAR(512), external_executable_path VARCHAR(2048), external_arguments VARCHAR(4096), external_working_directory VARCHAR(2048), external_run_elevated INTEGER, external_embed_window INTEGER, external_wait_for_exit INTEGER, parent_folder_id VARCHAR(36), sort_order INTEGER NOT NULL, options_json VARCHAR(8192), gateway_credential_provider VARCHAR(128), gateway_credential_identifier VARCHAR(2048))";
    protected override string CreateFoldersTableSql => "CREATE TABLE connection_folders (id VARCHAR(36) NOT NULL PRIMARY KEY, name VARCHAR(512) NOT NULL, parent_folder_id VARCHAR(36), sort_order INTEGER NOT NULL, options_json VARCHAR(8192), is_root INTEGER NOT NULL DEFAULT 0)";
}
