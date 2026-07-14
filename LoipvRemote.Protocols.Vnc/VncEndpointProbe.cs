using System.Net.Sockets;

namespace LoipvRemote.Protocols.Vnc;

/// <summary>Per-session VNC endpoint probe with no shared mutable connection state.</summary>
public sealed class VncEndpointProbe : IVncEndpointProbe
{
    public async Task ProbeAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        using var client = new TcpClient();

        try
        {
            await client.ConnectAsync(host, port, linkedSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Connection timed out to host {host} on port {port}.");
        }
    }
}
