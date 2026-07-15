using System.Data.Odbc;
using LoipvRemote.Properties;
using LoipvRemote.UseCases.Configuration;
using LoipvRemote.UseCases.Credentials;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;

namespace LoipvRemote.App.Configuration;

/// <summary>Desktop settings adapter for the selected connection persistence backend.</summary>
public sealed class DesktopConnectionStoreOptionsProvider(IStringSecretStore secretStore) : IConnectionStoreOptionsProvider
{
    private readonly IStringSecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));

    public ConnectionDefinitionStoreOptions GetOptions(bool useDatabase, string connectionFileName)
    {
        if (!useDatabase)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionFileName);
            return new ConnectionDefinitionStoreOptions(ConnectionDefinitionStoreKind.Xml, connectionFileName);
        }

        string databaseType = OptionsDBsPage.Default.SQLServerType.Trim().ToLowerInvariant();
        string password = _secretStore.Unprotect(OptionsDBsPage.Default.SQLPass, SecretPurposes.SqlPassword);
        return databaseType switch
        {
            "mssql" => new ConnectionDefinitionStoreOptions(ConnectionDefinitionStoreKind.SqlServer, CreateSqlServerConnectionString(password)),
            "mysql" => new ConnectionDefinitionStoreOptions(ConnectionDefinitionStoreKind.MySql, CreateMySqlConnectionString(password)),
            "odbc" => new ConnectionDefinitionStoreOptions(ConnectionDefinitionStoreKind.Odbc, CreateOdbcConnectionString()),
            "sqlite" => new ConnectionDefinitionStoreOptions(ConnectionDefinitionStoreKind.Sqlite, OptionsDBsPage.Default.SQLHost),
            _ => throw new NotSupportedException($"Connection store backend '{databaseType}' is not supported.")
        };
    }

    private static string CreateSqlServerConnectionString(string password)
    {
        string[] hostParts = OptionsDBsPage.Default.SQLHost.Split(':', 2);
        SqlConnectionStringBuilder builder = new()
        {
            ApplicationName = "LoipvRemote",
            DataSource = hostParts.Length == 2 ? $"{hostParts[0]},{hostParts[1]}" : OptionsDBsPage.Default.SQLHost,
            InitialCatalog = OptionsDBsPage.Default.SQLDatabaseName,
            IntegratedSecurity = string.IsNullOrWhiteSpace(OptionsDBsPage.Default.SQLUser),
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 30
        };
        if (!builder.IntegratedSecurity)
        {
            builder.UserID = OptionsDBsPage.Default.SQLUser;
            builder.Password = password;
        }

        return builder.ConnectionString;
    }

    private static string CreateMySqlConnectionString(string password)
    {
        string[] hostParts = OptionsDBsPage.Default.SQLHost.Split(':', 2);
        return new MySqlConnectionStringBuilder
        {
            Server = hostParts[0],
            Port = hostParts.Length == 2 ? uint.Parse(hostParts[1], System.Globalization.CultureInfo.InvariantCulture) : 3306,
            Database = OptionsDBsPage.Default.SQLDatabaseName,
            UserID = OptionsDBsPage.Default.SQLUser,
            Password = password
        }.ConnectionString;
    }

    private static string CreateOdbcConnectionString()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(OptionsDBsPage.Default.SQLHost);
        return new OdbcConnectionStringBuilder { ConnectionString = OptionsDBsPage.Default.SQLHost }.ConnectionString;
    }
}
