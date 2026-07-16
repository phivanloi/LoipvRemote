using System.Management;
using System.Net;
using System.Security.Principal;
using Microsoft.Win32;

namespace LoipvRemote.Infrastructure.Windows.Registry;

public sealed record PuttyRegistrySession(
    string Name,
    string Hostname,
    string Username,
    string Protocol,
    int Port,
    int SshVersion);

public sealed class PuttyRegistrySessionStore : IDisposable
{
    public const string SessionsPath = @"Software\SimonTatham\PuTTY\Sessions";
    private ManagementEventWatcher? _watcher;

    public event EventHandler? Changed;

    public static string[] GetSessionNames(bool raw = false)
    {
        using RegistryKey? sessionsKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SessionsPath);
        if (sessionsKey is null) return [];

        List<string> names = sessionsKey.GetSubKeyNames()
            .Select(name => raw ? name : DecodeName(name))
            .ToList();
        string defaultName = raw ? "Default%20Settings" : "Default Settings";
        if (!names.Contains(defaultName, StringComparer.Ordinal)) names.Insert(0, defaultName);
        return [.. names];
    }

    public static PuttyRegistrySession? GetSession(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName)) return null;
        using RegistryKey? sessionsKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SessionsPath);
        using RegistryKey? sessionKey = sessionsKey?.OpenSubKey(sessionName);
        if (sessionKey is null) return null;

        int port = int.TryParse(sessionKey.GetValue("PortNumber")?.ToString(), out int parsedPort)
            ? parsedPort
            : 0;
        int sshVersion = int.TryParse(sessionKey.GetValue("SshProt")?.ToString(), out int parsedSshVersion)
            ? parsedSshVersion
            : 0;
        return new PuttyRegistrySession(
            DecodeName(sessionName),
            sessionKey.GetValue("HostName")?.ToString() ?? string.Empty,
            sessionKey.GetValue("UserName")?.ToString() ?? string.Empty,
            sessionKey.GetValue("Protocol")?.ToString() ?? "ssh",
            port,
            sshVersion);
    }

    public static IReadOnlyList<PuttyRegistrySession> GetSessions()
    {
        List<PuttyRegistrySession> sessions = [];
        foreach (string rawName in GetSessionNames(raw: true))
        {
            if (rawName.EndsWith("Default%20Settings", StringComparison.Ordinal)) continue;
            PuttyRegistrySession? session = GetSession(rawName);
            if (session is not null && !string.IsNullOrWhiteSpace(session.Hostname)) sessions.Add(session);
        }
        return sessions;
    }

    public void StartWatcher()
    {
        if (_watcher is not null) return;
        string sid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("The current Windows user has no SID.");
        string keyName = string.Join("\\", sid, SessionsPath).Replace("\\", "\\\\", StringComparison.Ordinal);
        using RegistryKey? sessionsKey = Microsoft.Win32.Registry.Users.OpenSubKey(keyName);
        if (sessionsKey is null)
        {
            using RegistryKey _ = Microsoft.Win32.Registry.Users.CreateSubKey(keyName);
        }

        WqlEventQuery query = new(
            $"SELECT * FROM RegistryTreeChangeEvent WHERE Hive = 'HKEY_USERS' AND RootPath = '{keyName}'");
        _watcher = new ManagementEventWatcher(query);
        _watcher.EventArrived += OnEventArrived;
        _watcher.Start();
    }

    public void StopWatcher()
    {
        if (_watcher is null) return;
        _watcher.EventArrived -= OnEventArrived;
        _watcher.Stop();
        _watcher.Dispose();
        _watcher = null;
    }

    public void Dispose() => StopWatcher();

    private void OnEventArrived(object? sender, EventArrivedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    private static string DecodeName(string name) => WebUtility.UrlDecode(name.Replace("+", "%2B", StringComparison.Ordinal));
}
