using Microsoft.Win32;

namespace LoipvRemote.Infrastructure.Windows.Registry;

/// <summary>Windows implementation that reads literal values without environment expansion.</summary>
public sealed class WindowsRegistryValueReader : IWindowsRegistryValueReader
{
    public string? GetCurrentUserString(string subKeyPath, string valueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subKeyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);

        using RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(subKeyPath, writable: false);
        return key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }
}
