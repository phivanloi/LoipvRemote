namespace LoipvRemote.Protocols.Browser;

public sealed record BrowserConnectionOptions(string Host, int Port, string Scheme, int DefaultPort)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Host);
        ArgumentException.ThrowIfNullOrWhiteSpace(Scheme);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Port, 65535);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(DefaultPort);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(DefaultPort, 65535);
    }
}
