using Microsoft.Data.Sqlite;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Validation;
using LoipvRemote.Infrastructure.Persistence;
using LoipvRemote.UseCases.Configuration;

namespace LoipvRemote.Infrastructure.Persistence.Sqlite;

/// <summary>SQLite implementation with a versioned, parameterized schema for connection definitions.</summary>
public sealed class SqliteConnectionDefinitionStore(string connectionString) : IConnectionDefinitionStore
{
    private const int SchemaVersion = 3;
    private readonly string _connectionString = !string.IsNullOrWhiteSpace(connectionString)
        ? connectionString
        : throw new ArgumentException("A SQLite connection string is required.", nameof(connectionString));

    public async Task<ConnectionTreeDefinition> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, host, port, protocol, credential_provider, credential_identifier,
                   external_display_name, external_executable_path, external_arguments,
                   external_working_directory, external_run_elevated, external_embed_window,
                   external_wait_for_exit, parent_folder_id, sort_order, options_json,
                   gateway_credential_provider, gateway_credential_identifier
            FROM connection_definitions
            ORDER BY rowid;
            """;

        var definitions = new List<ConnectionDefinition>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            definitions.Add(ReadDefinition(reader));

        await using SqliteCommand foldersCommand = connection.CreateCommand();
        foldersCommand.CommandText = "SELECT id, name, parent_folder_id, sort_order, options_json, is_root FROM connection_folders ORDER BY sort_order, rowid;";
        var folders = new List<ConnectionFolderDefinition>();
        await using SqliteDataReader folderReader = await foldersCommand.ExecuteReaderAsync(cancellationToken);
        while (await folderReader.ReadAsync(cancellationToken))
            folders.Add(new ConnectionFolderDefinition(
                Guid.Parse(folderReader.GetString(0)),
                folderReader.GetString(1),
                folderReader.IsDBNull(2) ? null : Guid.Parse(folderReader.GetString(2)),
                folderReader.GetInt32(3),
                folderReader.IsDBNull(4) ? null : ConnectionNodeOptionsJson.Deserialize(folderReader.GetString(4)),
                !folderReader.IsDBNull(5) && folderReader.GetInt64(5) != 0));

        var tree = new ConnectionTreeDefinition(folders, definitions);
        tree.Validate();
        return tree;
    }

    public async Task SaveAsync(ConnectionTreeDefinition tree, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tree);
        tree.Validate();

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (SqliteCommand deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM connection_definitions; DELETE FROM connection_folders;";
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (ConnectionFolderDefinition folder in tree.Folders)
            await InsertFolderAsync(connection, transaction, folder, cancellationToken);

        foreach (ConnectionDefinition definition in tree.Connections)
            await InsertAsync(connection, transaction, definition, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER NOT NULL
            );
            INSERT INTO schema_version(version)
            SELECT {SchemaVersion}
            WHERE NOT EXISTS (SELECT 1 FROM schema_version);
            CREATE TABLE IF NOT EXISTS connection_definitions (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                host TEXT NOT NULL,
                port INTEGER NOT NULL,
                protocol TEXT NOT NULL,
                credential_provider TEXT NOT NULL,
                credential_identifier TEXT NOT NULL,
                external_display_name TEXT NULL,
                external_executable_path TEXT NULL,
                external_arguments TEXT NULL,
                external_working_directory TEXT NULL,
                external_run_elevated INTEGER NULL,
                external_embed_window INTEGER NULL,
                external_wait_for_exit INTEGER NULL,
                parent_folder_id TEXT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0,
                options_json TEXT NULL,
                gateway_credential_provider TEXT NULL,
                gateway_credential_identifier TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS connection_folders (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                parent_folder_id TEXT NULL,
                sort_order INTEGER NOT NULL,
                options_json TEXT NULL,
                is_root INTEGER NOT NULL DEFAULT 0
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        command.Parameters.Clear();
        command.CommandText = "SELECT id, name, host, port, protocol, credential_provider, credential_identifier, external_display_name, external_executable_path, external_arguments, external_working_directory, external_run_elevated, external_embed_window, external_wait_for_exit, parent_folder_id, sort_order, options_json, gateway_credential_provider, gateway_credential_identifier FROM connection_definitions WHERE 1 = 0;";
        try
        {
            await using SqliteDataReader shapeReader = await command.ExecuteReaderAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            throw new InvalidDataException("SQLite connection database uses an unsupported legacy schema.", exception);
        }

        command.CommandText = "SELECT version FROM schema_version LIMIT 1;";
        object? versionValue = await command.ExecuteScalarAsync(cancellationToken);
        long version = Convert.ToInt64(versionValue, System.Globalization.CultureInfo.InvariantCulture);
        if (version != SchemaVersion)
            throw new InvalidDataException($"SQLite connection database uses unsupported schema version {version}; expected {SchemaVersion}.");
    }

    private static async Task InsertAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ConnectionDefinition definition,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO connection_definitions (
                id, name, host, port, protocol, credential_provider, credential_identifier,
                external_display_name, external_executable_path, external_arguments,
                external_working_directory, external_run_elevated, external_embed_window,
                external_wait_for_exit, parent_folder_id, sort_order, options_json,
                gateway_credential_provider, gateway_credential_identifier)
            VALUES (
                $id, $name, $host, $port, $protocol, $credentialProvider, $credentialIdentifier,
                $externalDisplayName, $externalExecutablePath, $externalArguments,
                $externalWorkingDirectory, $externalRunElevated, $externalEmbedWindow,
                $externalWaitForExit, $parentFolderId, $sortOrder, $options,
                $gatewayCredentialProvider, $gatewayCredentialIdentifier);
            """;
        command.Parameters.AddWithValue("$id", definition.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", definition.Name);
        command.Parameters.AddWithValue("$host", definition.Host);
        command.Parameters.AddWithValue("$port", definition.Port);
        command.Parameters.AddWithValue("$protocol", definition.Protocol.ToString());
        command.Parameters.AddWithValue("$credentialProvider", definition.Credential.Provider);
        command.Parameters.AddWithValue("$credentialIdentifier", definition.Credential.Identifier);
        AddExternalApplicationParameters(command, definition.ExternalApplication);
        command.Parameters.AddWithValue("$parentFolderId", (object?)definition.ParentFolderId?.ToString("D") ?? DBNull.Value);
        command.Parameters.AddWithValue("$sortOrder", definition.SortOrder);
        command.Parameters.AddWithValue("$options", (object?)ConnectionNodeOptionsJson.Serialize(definition.Options) ?? DBNull.Value);
        command.Parameters.AddWithValue("$gatewayCredentialProvider", (object?)definition.GatewayCredential?.Provider ?? DBNull.Value);
        command.Parameters.AddWithValue("$gatewayCredentialIdentifier", (object?)definition.GatewayCredential?.Identifier ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertFolderAsync(SqliteConnection connection, SqliteTransaction transaction, ConnectionFolderDefinition folder, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO connection_folders (id, name, parent_folder_id, sort_order, options_json, is_root) VALUES ($id, $name, $parentFolderId, $sortOrder, $options, $isRoot);";
        command.Parameters.AddWithValue("$id", folder.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", folder.Name);
        command.Parameters.AddWithValue("$parentFolderId", (object?)folder.ParentFolderId?.ToString("D") ?? DBNull.Value);
        command.Parameters.AddWithValue("$sortOrder", folder.SortOrder);
        command.Parameters.AddWithValue("$options", (object?)ConnectionNodeOptionsJson.Serialize(folder.Options) ?? DBNull.Value);
        command.Parameters.AddWithValue("$isRoot", folder.IsRoot ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddExternalApplicationParameters(SqliteCommand command, ExternalApplicationDefinition? definition)
    {
        command.Parameters.AddWithValue("$externalDisplayName", (object?)definition?.DisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("$externalExecutablePath", (object?)definition?.ExecutablePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$externalArguments", (object?)definition?.Arguments ?? DBNull.Value);
        command.Parameters.AddWithValue("$externalWorkingDirectory", (object?)definition?.WorkingDirectory ?? DBNull.Value);
        command.Parameters.AddWithValue("$externalRunElevated", definition is null ? DBNull.Value : definition.RunElevated ? 1 : 0);
        command.Parameters.AddWithValue("$externalEmbedWindow", definition is null ? DBNull.Value : definition.EmbedWindow ? 1 : 0);
        command.Parameters.AddWithValue("$externalWaitForExit", definition is null ? DBNull.Value : definition.WaitForExit ? 1 : 0);
    }

    private static ConnectionDefinition ReadDefinition(SqliteDataReader reader)
    {
        if (!Guid.TryParse(reader.GetString(0), out Guid id))
            throw new InvalidDataException("SQLite connection definition has an invalid id.");
        if (!Enum.TryParse(reader.GetString(4), out ProtocolKind protocol))
            throw new InvalidDataException("SQLite connection definition has an invalid protocol.");

        ExternalApplicationDefinition? externalApplication = ReadExternalApplication(reader, protocol);
        var definition = new ConnectionDefinition(
            id,
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            protocol,
            new CredentialReference(reader.GetString(5), reader.GetString(6)),
            externalApplication,
            reader.IsDBNull(14) ? null : Guid.Parse(reader.GetString(14)),
            reader.GetInt32(15),
            reader.IsDBNull(16) ? null : ConnectionNodeOptionsJson.Deserialize(reader.GetString(16)),
            ReadGatewayCredential(reader));
        ConnectionDefinitionValidator.Validate(definition);
        return definition;
    }

    private static ExternalApplicationDefinition? ReadExternalApplication(SqliteDataReader reader, ProtocolKind protocol)
    {
        bool hasExternalValues = Enumerable.Range(7, 7).Any(index => !reader.IsDBNull(index));
        if (protocol != ProtocolKind.ExternalApplication)
        {
            if (hasExternalValues)
                throw new InvalidDataException("Only external application connections may contain external application settings.");

            return null;
        }

        if (Enumerable.Range(7, 7).Any(index => reader.IsDBNull(index)))
            throw new InvalidDataException("External application connection is missing application settings.");

        return new ExternalApplicationDefinition(
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetInt64(11) != 0,
            reader.GetInt64(12) != 0,
            reader.GetInt64(13) != 0);
    }

    private static CredentialReference? ReadGatewayCredential(SqliteDataReader reader)
    {
        bool hasProvider = !reader.IsDBNull(17);
        bool hasIdentifier = !reader.IsDBNull(18);
        if (!hasProvider && !hasIdentifier)
            return null;
        if (!hasProvider || !hasIdentifier)
            throw new InvalidDataException("SQLite connection definition has an incomplete gateway credential reference.");

        return new CredentialReference(reader.GetString(17), reader.GetString(18));
    }
}
