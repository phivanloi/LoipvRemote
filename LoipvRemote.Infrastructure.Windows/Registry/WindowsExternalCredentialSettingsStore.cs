using LoipvRemote.Protocols.Abstractions;
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace LoipvRemote.Infrastructure.Windows.Registry;

[SupportedOSPlatform("windows")]
public sealed class WindowsExternalCredentialSettingsStore : IExternalCredentialSettingsStore
{
    private const string Root = "SOFTWARE\\LoipvRemote\\Connectors";

    public string? GetString(string scope, string name)
    {
        using RegistryKey key = Open(scope, writable: false);
        return key.GetValue(name) as string;
    }

    public bool GetBoolean(string scope, string name, bool defaultValue = false)
    {
        string? value = GetString(scope, name);
        return value is null ? defaultValue : bool.TryParse(value, out bool parsed) ? parsed : defaultValue;
    }

    public void SetString(string scope, string name, string value)
    {
        using RegistryKey key = Open(scope, writable: true);
        key.SetValue(name, value);
    }

    public void SetBoolean(string scope, string name, bool value) => SetString(scope, name, value.ToString());

    private static RegistryKey Open(string scope, bool writable)
    {
        RegistryKey root = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(Root, writable: true)
            ?? throw new InvalidOperationException("Unable to open LoipvRemote connector settings.");
        return root.CreateSubKey(scope, writable)
            ?? throw new InvalidOperationException($"Unable to open connector settings scope '{scope}'.");
    }
}
