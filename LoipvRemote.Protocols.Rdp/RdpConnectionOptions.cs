using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Rdp;

public sealed record RdpConnectionOptions(
    string Host,
    int Port,
    string Username = "",
    string Password = "",
    string Domain = "",
    RdpGatewayConfiguration? Gateway = null,
    RdpRuntimeConfiguration? RuntimeConfiguration = null,
    RdpDisplayConfiguration? DisplayConfiguration = null)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Port, 65535);
    }
}
