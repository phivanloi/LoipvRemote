using System.Net.Sockets;

namespace LoipvRemote.Protocols.Putty;

/// <summary>Checks endpoint reachability before PuTTY can show a native fatal-error dialog.</summary>
public sealed class PuttyEndpointProbe : IPuttyEndpointProbe
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
