namespace LoipvRemote.UseCases.Credentials;

public static class ConnectionSecretPurposes
{
    public const string ConnectionSecretPrefix = "connection-secret";

    public static string ForConnectionOption(string connectionId, string propertyName) =>
        $"{ConnectionSecretPrefix}:{connectionId}:{propertyName}";
}
