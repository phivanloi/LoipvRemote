namespace LoipvRemote.Infrastructure.Persistence.Connectors
{
    public enum ConnectionTestResult
    {
        ConnectionSucceded,
        ServerNotAccessible,
        UnknownDatabase,
        CredentialsRejected,
        UnknownError
    }
}
