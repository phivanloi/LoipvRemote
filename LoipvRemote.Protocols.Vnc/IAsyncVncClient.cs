namespace LoipvRemote.Protocols.Vnc;

/// <summary>Optional asynchronous transport contract for native VNC implementations.</summary>
public interface IAsyncVncClient
{
    ValueTask ConnectAsync(string host, bool viewOnly, bool smartSize, CancellationToken cancellationToken = default);
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);
}
