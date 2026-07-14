namespace LoipvRemote.Protocols.Vnc;

public sealed record VncConnectionOptions(string Host, int Port, bool ViewOnly, bool SmartSize)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Port, 65535);
    }
}
