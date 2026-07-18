using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LoipvRemote.Protocols.Putty;

/// <summary>
/// Creates a one-use PuTTY saved session whose terminal font is normalized for
/// the DPI of the WinUI native host. User saved sessions are never modified.
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
            return null;

        string sourceName = string.IsNullOrWhiteSpace(sourceSessionName) ? "Default Settings" : sourceSessionName;
        string sessionName = $"LoipvRemote DPI {Guid.NewGuid():N}";
        string registrySessionName = EscapeSessionName(sessionName);
        try
        {
            using RegistryKey sessions = Registry.CurrentUser.CreateSubKey(SessionsRegistryPath, writable: true);
            using RegistryKey? source = sessions.OpenSubKey(EscapeSessionName(sourceName), writable: false);
            using RegistryKey target = sessions.CreateSubKey(registrySessionName, writable: true);
            CopyValues(source, target);

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
        // PuTTY's default 12px font is visually too large when hosted in the
        // compact WinUI terminal area. Use a 10px baseline at 100% DPI while
        // still respecting deliberately smaller saved fonts.
        int baseHeight = Math.Clamp(savedFontHeight, 5, 10);
        uint effectiveDpi = Math.Max(dpi, 96);
        return Math.Clamp(
            (int)Math.Round(baseHeight * 96d / effectiveDpi, MidpointRounding.AwayFromZero),
            5,
            24);
    }
}
