namespace LoipvRemote.WinUI.Sessions;

internal static class PortableExportPolicy
{
    public static string GetWarning(int connectionCount) =>
        $"This portable XML will contain plaintext credentials for {connectionCount} connections. " +
        "Export only to a secure location, remove the file after use, and do not send it through untrusted channels.";
}
