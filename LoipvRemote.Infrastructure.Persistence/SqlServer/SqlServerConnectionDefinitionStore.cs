using System.Data.Common;
using Microsoft.Data.SqlClient;
using LoipvRemote.Infrastructure.Persistence.Relational;

namespace LoipvRemote.Infrastructure.Persistence.SqlServer;

public sealed class SqlServerConnectionDefinitionStore(string connectionString) : RelationalConnectionDefinitionStore(connectionString)
{
    protected override DbConnection CreateConnection(string connectionString) => new SqlConnection(connectionString);
    protected override string CreateSchemaVersionTableSql => "IF OBJECT_ID(N'connection_schema_version', N'U') IS NULL CREATE TABLE connection_schema_version (id INT NOT NULL PRIMARY KEY, version INT NOT NULL)";
    protected override string CreateTableSql => "IF OBJECT_ID(N'connection_definitions', N'U') IS NULL CREATE TABLE connection_definitions (id NVARCHAR(36) NOT NULL PRIMARY KEY, name NVARCHAR(512) NOT NULL, host NVARCHAR(2048) NOT NULL, port INT NOT NULL, protocol NVARCHAR(64) NOT NULL, credential_provider NVARCHAR(128) NOT NULL, credential_identifier NVARCHAR(2048) NOT NULL, parent_folder_id NVARCHAR(36) NULL, sort_order INT NOT NULL DEFAULT 0, options_json NVARCHAR(MAX) NULL, gateway_credential_provider NVARCHAR(128) NULL, gateway_credential_identifier NVARCHAR(2048) NULL)";
    protected override string CreateFoldersTableSql => "IF OBJECT_ID(N'connection_folders', N'U') IS NULL CREATE TABLE connection_folders (id NVARCHAR(36) NOT NULL PRIMARY KEY, name NVARCHAR(512) NOT NULL, parent_folder_id NVARCHAR(36) NULL, sort_order INT NOT NULL, options_json NVARCHAR(MAX) NULL, is_root INT NOT NULL DEFAULT 0)";
}
