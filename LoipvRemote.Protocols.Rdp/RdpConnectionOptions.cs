namespace LoipvRemote.Protocols.Rdp;

public sealed record RdpConnectionOptions(string Host, int Port)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Port, 65535);
    }
}
