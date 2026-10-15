namespace LoipvRemote.Protocols.Putty;

/// <summary>Validated launch contract for a PuTTY protocol session.</summary>
public sealed record PuttyConnectionOptions(
    string ExecutablePath,
    PuttyLaunchOptions LaunchOptions,
    bool StartMinimized = false)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ExecutablePath);
        ArgumentNullException.ThrowIfNull(LaunchOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(LaunchOptions.Hostname);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(LaunchOptions.Port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(LaunchOptions.Port, 65535);
    }
}
