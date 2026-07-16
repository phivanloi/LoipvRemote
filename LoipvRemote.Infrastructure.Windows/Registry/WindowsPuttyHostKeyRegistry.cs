using LoipvRemote.Protocols.Putty.Monitoring;

namespace LoipvRemote.Infrastructure.Windows.Registry;

/// <summary>Windows registry adapter consumed by the Putty protocol module.</summary>
public sealed class WindowsPuttyHostKeyRegistry(IWindowsRegistryValueReader reader) : IPuttyHostKeyRegistry
{
    private readonly IWindowsRegistryValueReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    public string? GetCurrentUserString(string subKeyPath, string valueName) =>
        _reader.GetCurrentUserString(subKeyPath, valueName);
}
