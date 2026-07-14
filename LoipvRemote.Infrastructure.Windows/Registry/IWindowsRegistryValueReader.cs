namespace LoipvRemote.Infrastructure.Windows.Registry;

/// <summary>Read-only Windows Registry boundary for protocol and configuration adapters.</summary>
public interface IWindowsRegistryValueReader
{
    string? GetCurrentUserString(string subKeyPath, string valueName);
}
