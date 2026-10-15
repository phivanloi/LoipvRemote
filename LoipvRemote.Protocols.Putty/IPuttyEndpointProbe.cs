namespace LoipvRemote.Protocols.Putty;

public interface IPuttyEndpointProbe
{
    Task ProbeAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default);
}
