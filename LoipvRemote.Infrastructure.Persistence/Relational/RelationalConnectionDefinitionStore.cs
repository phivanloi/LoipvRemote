using System.Data.Common;
using System.Globalization;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Validation;
using LoipvRemote.Infrastructure.Persistence;
using LoipvRemote.UseCases.Configuration;

namespace LoipvRemote.Infrastructure.Persistence.Relational;

/// <summary>Provider-neutral transactional store for secret-free connection definitions.</summary>
public abstract class RelationalConnectionDefinitionStore : IConnectionDefinitionStore
{
    private const int SchemaVersion = 3;
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
        command.CommandText = "SELECT id, name, host, port, protocol, credential_provider, credential_identifier, external_display_name, external_executable_path, external_arguments, external_working_directory, external_run_elevated, external_embed_window, external_wait_for_exit, parent_folder_id, sort_order, options_json, gateway_credential_provider, gateway_credential_identifier FROM connection_definitions ORDER BY sort_order, id";
        var definitions = new List<ConnectionDefinition>();
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!Guid.TryParse(reader.GetString(0), out Guid id) || !Enum.TryParse(reader.GetString(4), out ProtocolKind protocol))
                throw new InvalidDataException("The relational connection definition contains an invalid id or protocol.");

            var definition = new ConnectionDefinition(id, reader.GetString(1), reader.GetString(2), reader.GetInt32(3), protocol,
                new CredentialReference(reader.GetString(5), reader.GetString(6)), ReadExternalApplication(reader, protocol),
                reader.IsDBNull(14) ? null : Guid.Parse(reader.GetString(14)), Convert.ToInt32(reader.GetValue(15), CultureInfo.InvariantCulture),
                reader.IsDBNull(16) ? null : ConnectionNodeOptionsJson.Deserialize(reader.GetString(16)),
                ReadGatewayCredential(reader));
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
                insert.CommandText = "INSERT INTO connection_definitions (id, name, host, port, protocol, credential_provider, credential_identifier, external_display_name, external_executable_path, external_arguments, external_working_directory, external_run_elevated, external_embed_window, external_wait_for_exit, parent_folder_id, sort_order, options_json, gateway_credential_provider, gateway_credential_identifier) VALUES (@id, @name, @host, @port, @protocol, @provider, @identifier, @externalDisplayName, @externalExecutablePath, @externalArguments, @externalWorkingDirectory, @externalRunElevated, @externalEmbedWindow, @externalWaitForExit, @parentFolderId, @sortOrder, @options, @gatewayProvider, @gatewayIdentifier)";
                AddParameter(insert, "@id", definition.Id.ToString("D"));
                AddParameter(insert, "@name", definition.Name);
                AddParameter(insert, "@host", definition.Host);
                AddParameter(insert, "@port", definition.Port);
                AddParameter(insert, "@protocol", definition.Protocol.ToString());
                AddParameter(insert, "@provider", definition.Credential.Provider);
                AddParameter(insert, "@identifier", definition.Credential.Identifier);
                AddParameter(insert, "@externalDisplayName", (object?)definition.ExternalApplication?.DisplayName ?? DBNull.Value);
                AddParameter(insert, "@externalExecutablePath", (object?)definition.ExternalApplication?.ExecutablePath ?? DBNull.Value);
                AddParameter(insert, "@externalArguments", (object?)definition.ExternalApplication?.Arguments ?? DBNull.Value);
                AddParameter(insert, "@externalWorkingDirectory", (object?)definition.ExternalApplication?.WorkingDirectory ?? DBNull.Value);
                AddParameter(insert, "@externalRunElevated", definition.ExternalApplication is null ? DBNull.Value : definition.ExternalApplication.RunElevated ? 1 : 0);
                AddParameter(insert, "@externalEmbedWindow", definition.ExternalApplication is null ? DBNull.Value : definition.ExternalApplication.EmbedWindow ? 1 : 0);
                AddParameter(insert, "@externalWaitForExit", definition.ExternalApplication is null ? DBNull.Value : definition.ExternalApplication.WaitForExit ? 1 : 0);
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
                // ODBC has no portable CREATE TABLE IF NOT EXISTS syntax.
            }
        }

        // The application deliberately does not migrate the removed legacy schema.
        // Validate the complete current column shape before registering a new version
        // marker, otherwise an older database could be mistaken for the current model.
        command.Parameters.Clear();
        command.CommandText = "SELECT id, name, host, port, protocol, credential_provider, credential_identifier, external_display_name, external_executable_path, external_arguments, external_working_directory, external_run_elevated, external_embed_window, external_wait_for_exit, parent_folder_id, sort_order, options_json, gateway_credential_provider, gateway_credential_identifier FROM connection_definitions WHERE 1 = 0";
        try
        {
            await using DbDataReader shapeReader = await command.ExecuteReaderAsync(cancellationToken);
        }
        catch (DbException exception)
        {
            throw new InvalidDataException("The relational connection database uses an unsupported legacy schema.", exception);
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
        if (version != SchemaVersion)
            throw new InvalidDataException($"Relational connection database uses schema version {version}; expected {SchemaVersion}.");
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static ExternalApplicationDefinition? ReadExternalApplication(DbDataReader reader, ProtocolKind protocol)
    {
        bool hasExternalValues = Enumerable.Range(7, 7).Any(index => !reader.IsDBNull(index));
        if (protocol != ProtocolKind.ExternalApplication)
        {
            if (hasExternalValues)
                throw new InvalidDataException("Only external application connections may contain external application settings.");
            return null;
        }

        if (Enumerable.Range(7, 7).Any(reader.IsDBNull))
            throw new InvalidDataException("External application connection is missing application settings.");

        return new ExternalApplicationDefinition(reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10),
            Convert.ToInt32(reader.GetValue(11), CultureInfo.InvariantCulture) != 0,
            Convert.ToInt32(reader.GetValue(12), CultureInfo.InvariantCulture) != 0,
            Convert.ToInt32(reader.GetValue(13), CultureInfo.InvariantCulture) != 0);
    }

    private static CredentialReference? ReadGatewayCredential(DbDataReader reader)
    {
        bool hasProvider = !reader.IsDBNull(17);
        bool hasIdentifier = !reader.IsDBNull(18);
        if (!hasProvider && !hasIdentifier)
            return null;
        if (!hasProvider || !hasIdentifier)
            throw new InvalidDataException("The relational connection definition has an incomplete gateway credential reference.");

        return new CredentialReference(reader.GetString(17), reader.GetString(18));
    }
}
