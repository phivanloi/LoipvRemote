using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LoipvRemote.Protocols.Putty;

/// <summary>
/// Tạo cấu hình PuTTY dùng một lần với font phù hợp DPI và keepalive mặc định.
/// Không sửa cấu hình PuTTY đã lưu của người dùng.
/// </summary>
public sealed class PuttyDpiSettingsSession : IDisposable
{
    private const string SessionsRegistryPath = @"Software\SimonTatham\PuTTY\Sessions";
    private readonly string _registrySessionName;
    private bool _disposed;

    private PuttyDpiSettingsSession(string sessionName, string registrySessionName, uint dpi)
    {
        SessionName = sessionName;
        _registrySessionName = registrySessionName;
        Dpi = dpi;
    }

    public string SessionName { get; }
    public uint Dpi { get; }

    public static PuttyDpiSettingsSession? TryCreate(string sourceSessionName, IntPtr hostWindowHandle)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        uint dpi = GetDpiForWindow(hostWindowHandle);
        if (dpi == 0)
            dpi = 96;

        string sourceName = string.IsNullOrWhiteSpace(sourceSessionName) ? "Default Settings" : sourceSessionName;
        string sessionName = $"LoipvRemote DPI {Guid.NewGuid():N}";
        string registrySessionName = EscapeSessionName(sessionName);
        try
        {
            using RegistryKey sessions = Registry.CurrentUser.CreateSubKey(SessionsRegistryPath, writable: true);
            using RegistryKey? source = sessions.OpenSubKey(EscapeSessionName(sourceName), writable: false);
            using RegistryKey target = sessions.CreateSubKey(registrySessionName, writable: true);
            CopyValues(source, target);
            ApplyDefaultKeepalive(target, sourceSessionName);

            int sourceFontHeight = source?.GetValue("FontHeight") is int fontHeight && fontHeight > 0
                ? fontHeight
                : 10;
            target.SetValue("FontHeight", PuttyFontScaling.GetFontHeight(sourceFontHeight, dpi), RegistryValueKind.DWord);
            return new PuttyDpiSettingsSession(sessionName, registrySessionName, dpi);
        }
        catch
        {
            TryDelete(registrySessionName);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (OperatingSystem.IsWindows())
            TryDelete(_registrySessionName);
        GC.SuppressFinalize(this);
    }

    [SupportedOSPlatform("windows")]
    internal static void ApplyDefaultKeepalive(RegistryKey target, string sourceSessionName)
    {
        // Imported connections can explicitly name PuTTY's default profile.
        // A genuinely custom saved session owns its keepalive policy, even 0.
        if (!string.IsNullOrWhiteSpace(sourceSessionName) &&
            !string.Equals(sourceSessionName, "Default Settings", StringComparison.OrdinalIgnoreCase))
            return;

        // PuTTY stores the interval as minutes plus seconds. Preserve an
        // existing non-zero interval; only replace its disabled default.
        if (target.GetValue("PingInterval") is int minutes && minutes > 0 ||
            target.GetValue("PingIntervalSecs") is int seconds && seconds > 0)
            return;

        target.SetValue("PingInterval", 0, RegistryValueKind.DWord);
        target.SetValue("PingIntervalSecs", 30, RegistryValueKind.DWord);
    }

    [SupportedOSPlatform("windows")]
    private static void CopyValues(RegistryKey? source, RegistryKey target)
    {
        if (source is null)
            return;

        foreach (string name in source.GetValueNames())
        {
            object? value = source.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (value is not null)
                target.SetValue(name, value, source.GetValueKind(name));
        }
    }

    private static string EscapeSessionName(string sessionName) => Uri.EscapeDataString(sessionName);

    private static void TryDelete(string registrySessionName)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            using RegistryKey? sessions = Registry.CurrentUser.OpenSubKey(SessionsRegistryPath, writable: true);
            sessions?.DeleteSubKeyTree(registrySessionName, throwOnMissingSubKey: false);
        }
        catch (UnauthorizedAccessException)
        {
            // A locked-down registry must not block the SSH connection. PuTTY
            // falls back to the user's normal saved session in that case.
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr windowHandle);
}

/// <summary>Pure DPI-to-PuTTY-font conversion used by the temporary session.</summary>
public static class PuttyFontScaling
{
    public static int GetFontHeight(int savedFontHeight, uint dpi)
    {
        // PuTTY already renders its saved pixel height in the DPI context of
        // its own HWND. Scaling that value down again makes terminal text
        // progressively smaller than WinUI text. Preserve the user's logical
        // size and only constrain extremes that would be unusable when hosted.
        _ = dpi;
        return Math.Clamp(savedFontHeight, 10, 18);
    }
}
