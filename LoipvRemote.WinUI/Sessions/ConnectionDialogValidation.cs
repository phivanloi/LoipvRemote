using LoipvRemote.Domain.Connections;

namespace LoipvRemote.WinUI.Sessions;

internal static class ConnectionDialogValidation
{
    public static string? GetError(string name, string host, ProtocolKind? protocol, double port)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Enter a connection name.";
        if (string.IsNullOrWhiteSpace(host))
            return "Enter a host name or IP address.";
        if (protocol is null || !Enum.IsDefined(protocol.Value))
            return "Select a supported protocol.";
        if (!double.IsFinite(port) || port is < 1 or > 65535 || port != Math.Truncate(port))
            return "Enter a port between 1 and 65535.";

        return null;
    }

    public static string? GetQuickConnectError(string host, ProtocolKind? protocol, double port)
    {
        if (string.IsNullOrWhiteSpace(host))
            return "Enter a host name or IP address.";
        if (protocol is null || !Enum.IsDefined(protocol.Value))
            return "Select a supported protocol.";
        if (!double.IsFinite(port) || port is < 1 or > 65535 || port != Math.Truncate(port))
            return "Enter a port between 1 and 65535.";

        return null;
    }

    public static string? GetFolderError(string name) =>
        string.IsNullOrWhiteSpace(name) ? "Enter a folder name." : null;

    public static string? GetCredentialError(string name, string password)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Enter a credential name.";
        if (string.IsNullOrEmpty(password))
            return "Enter a password.";

        return null;
    }
}
