namespace LoipvRemote.Protocols.Vnc;

public interface IVncEndpointProbe
{
    Task ProbeAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default);
}
