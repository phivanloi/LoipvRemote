using System.Data.Common;
using System.Globalization;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Validation;
using LoipvRemote.Infrastructure.Persistence;
using LoipvRemote.Application.Configuration;

namespace LoipvRemote.Infrastructure.Persistence.Relational;

/// <summary>Provider-neutral transactional store for secret-free connection definitions.</summary>
public abstract class RelationalConnectionDefinitionStore : IConnectionDefinitionStore
{
    private const int SchemaVersion = 4;
    private readonly string _connectionString;

    protected RelationalConnectionDefinitionStore(string connectionString)
    {
        _connectionString = !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : throw new ArgumentException("A database connection string is required.", nameof(connectionString));
    }

    protected abstract DbConnection CreateConnection(string connectionString);
    protected abstract string CreateSchemaVersionTableSql { get; }
    protected abstract string CreateTableSql { get; }
    protected abstract string CreateFoldersTableSql { get; }
    protected virtual bool IgnoreExistingTableError => false;

    public async Task<ConnectionTreeDefinition> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, host, port, protocol, credential_provider, credential_identifier, parent_folder_id, sort_order, options_json, gateway_credential_provider, gateway_credential_identifier FROM connection_definitions ORDER BY sort_order, id";
        var definitions = new List<ConnectionDefinition>();
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!Guid.TryParse(reader.GetString(0), out Guid id) || !Enum.TryParse(reader.GetString(4), out ProtocolKind protocol))
                throw new InvalidDataException("The relational connection definition contains an invalid id or protocol.");

            var definition = new ConnectionDefinition(id, reader.GetString(1), reader.GetString(2), reader.GetInt32(3), protocol,
                new CredentialReference(reader.GetString(5), reader.GetString(6)),
                ParentFolderId: reader.IsDBNull(7) ? null : Guid.Parse(reader.GetString(7)),
                SortOrder: Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture),
                Options: reader.IsDBNull(9) ? null : ConnectionNodeOptionsJson.Deserialize(reader.GetString(9)),
                GatewayCredential: ReadGatewayCredential(reader));
            ConnectionDefinitionValidator.Validate(definition);
            definitions.Add(definition);
        }

        await using DbCommand foldersCommand = connection.CreateCommand();
        foldersCommand.CommandText = "SELECT id, name, parent_folder_id, sort_order, options_json, is_root FROM connection_folders ORDER BY sort_order, id";
        var folders = new List<ConnectionFolderDefinition>();
        await using DbDataReader foldersReader = await foldersCommand.ExecuteReaderAsync(cancellationToken);
        while (await foldersReader.ReadAsync(cancellationToken))
            folders.Add(new ConnectionFolderDefinition(Guid.Parse(foldersReader.GetString(0)), foldersReader.GetString(1),
                foldersReader.IsDBNull(2) ? null : Guid.Parse(foldersReader.GetString(2)), Convert.ToInt32(foldersReader.GetValue(3), CultureInfo.InvariantCulture),
                foldersReader.IsDBNull(4) ? null : ConnectionNodeOptionsJson.Deserialize(foldersReader.GetString(4)),
                !foldersReader.IsDBNull(5) && Convert.ToInt32(foldersReader.GetValue(5), CultureInfo.InvariantCulture) != 0));

        var tree = new ConnectionTreeDefinition(folders, definitions);
        tree.Validate();
        return tree;
    }

    public async Task SaveAsync(ConnectionTreeDefinition tree, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tree);
        tree.Validate();

        await using DbConnection connection = CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (string tableName in new[] { "connection_definitions", "connection_folders" })
            {
                await using DbCommand delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = $"DELETE FROM {tableName}";
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (ConnectionFolderDefinition folder in tree.Folders)
            {
                await using DbCommand insertFolder = connection.CreateCommand();
                insertFolder.Transaction = transaction;
                insertFolder.CommandText = "INSERT INTO connection_folders (id, name, parent_folder_id, sort_order, options_json, is_root) VALUES (@id, @name, @parentFolderId, @sortOrder, @options, @isRoot)";
                AddParameter(insertFolder, "@id", folder.Id.ToString("D"));
                AddParameter(insertFolder, "@name", folder.Name);
                AddParameter(insertFolder, "@parentFolderId", (object?)folder.ParentFolderId?.ToString("D") ?? DBNull.Value);
                AddParameter(insertFolder, "@sortOrder", folder.SortOrder);
                AddParameter(insertFolder, "@options", (object?)ConnectionNodeOptionsJson.Serialize(folder.Options) ?? DBNull.Value);
                AddParameter(insertFolder, "@isRoot", folder.IsRoot ? 1 : 0);
                await insertFolder.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (ConnectionDefinition definition in tree.Connections)
            {
                await using DbCommand insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO connection_definitions (id, name, host, port, protocol, credential_provider, credential_identifier, parent_folder_id, sort_order, options_json, gateway_credential_provider, gateway_credential_identifier) VALUES (@id, @name, @host, @port, @protocol, @provider, @identifier, @parentFolderId, @sortOrder, @options, @gatewayProvider, @gatewayIdentifier)";
                AddParameter(insert, "@id", definition.Id.ToString("D"));
                AddParameter(insert, "@name", definition.Name);
                AddParameter(insert, "@host", definition.Host);
                AddParameter(insert, "@port", definition.Port);
                AddParameter(insert, "@protocol", definition.Protocol.ToString());
                AddParameter(insert, "@provider", definition.Credential.Provider);
                AddParameter(insert, "@identifier", definition.Credential.Identifier);
                AddParameter(insert, "@parentFolderId", (object?)definition.ParentFolderId?.ToString("D") ?? DBNull.Value);
                AddParameter(insert, "@sortOrder", definition.SortOrder);
                AddParameter(insert, "@options", (object?)ConnectionNodeOptionsJson.Serialize(definition.Options) ?? DBNull.Value);
                AddParameter(insert, "@gatewayProvider", (object?)definition.GatewayCredential?.Provider ?? DBNull.Value);
                AddParameter(insert, "@gatewayIdentifier", (object?)definition.GatewayCredential?.Identifier ?? DBNull.Value);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        foreach (string createTableSql in new[] { CreateSchemaVersionTableSql, CreateTableSql, CreateFoldersTableSql })
        {
            command.CommandText = createTableSql;
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (DbException) when (IgnoreExistingTableError)
            {
                // Provider-specific stores may not support idempotent CREATE TABLE syntax.
            }
        }

        // The application deliberately does not migrate removed schemas.
        // Validate the complete current column shape before registering a new version
        // marker, otherwise an older database could be mistaken for the current model.
        command.Parameters.Clear();
        command.CommandText = "SELECT id, name, host, port, protocol, credential_provider, credential_identifier, parent_folder_id, sort_order, options_json, gateway_credential_provider, gateway_credential_identifier FROM connection_definitions WHERE 1 = 0";
        try
        {
            await using DbDataReader shapeReader = await command.ExecuteReaderAsync(cancellationToken);
        }
        catch (DbException exception)
        {
            throw new InvalidDataException("The relational connection database uses an unsupported schema.", exception);
        }

        command.Parameters.Clear();
        command.CommandText = "SELECT version FROM connection_schema_version WHERE id = 1";
        object? versionValue = await command.ExecuteScalarAsync(cancellationToken);
        if (versionValue is null or DBNull)
        {
            command.CommandText = "INSERT INTO connection_schema_version (id, version) VALUES (1, @version)";
            AddParameter(command, "@version", SchemaVersion);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        int version = Convert.ToInt32(versionValue, System.Globalization.CultureInfo.InvariantCulture);
        if (version == SchemaVersion)
            return;
        if (version != 3)
            throw new InvalidDataException($"Relational connection database uses schema version {version}; expected {SchemaVersion}.");

        // Version 4 removes the unused external-application contract. Existing
        // SQL Server rows retain their generic columns, but the runtime reads
        // only SSH/RDP/VNC fields and marks the schema as the current model.
        command.CommandText = "UPDATE connection_schema_version SET version = @version WHERE id = 1";
        AddParameter(command, "@version", SchemaVersion);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static CredentialReference? ReadGatewayCredential(DbDataReader reader)
    {
        bool hasProvider = !reader.IsDBNull(10);
        bool hasIdentifier = !reader.IsDBNull(11);
        if (!hasProvider && !hasIdentifier)
            return null;
        if (!hasProvider || !hasIdentifier)
            throw new InvalidDataException("The relational connection definition has an incomplete gateway credential reference.");

        return new CredentialReference(reader.GetString(10), reader.GetString(11));
    }
}
