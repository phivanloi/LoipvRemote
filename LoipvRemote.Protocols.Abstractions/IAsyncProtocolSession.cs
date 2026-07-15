namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Optional asynchronous lifecycle implemented by fully migrated protocol sessions.</summary>
public interface IAsyncProtocolSession : IProtocolSession, IAsyncDisposable
{
    ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default);
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);
    ValueTask CloseAsync(CancellationToken cancellationToken = default);
}
