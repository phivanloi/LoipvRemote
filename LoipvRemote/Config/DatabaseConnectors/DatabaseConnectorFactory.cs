using LoipvRemote.Security.SymmetricEncryption;
using System;
using System.Runtime.Versioning;

namespace LoipvRemote.Config.DatabaseConnectors
{
    [SupportedOSPlatform("windows")]
    public static class DatabaseConnectorFactory
    {
        public static IDatabaseConnector DatabaseConnector(string type, string server, string database, string username, string password)
        {
            return type switch
            {
                "mysql" => new MySqlDatabaseConnector(server, database, username, password),
                "odbc" => throw new NotSupportedException("ODBC database connections are not supported for schema initialization. Please use a supported database backend."),
                "mssql" => new MSSqlDatabaseConnector(server, database, username, password),
                _ => new MSSqlDatabaseConnector(server, database, username, password)
            };
        }
    }
}
