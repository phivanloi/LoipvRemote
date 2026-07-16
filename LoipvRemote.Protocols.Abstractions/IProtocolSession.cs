using LoipvRemote.Domain.Protocols;

namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Lifecycle contract shared by every remote protocol session.</summary>
public interface IProtocolSession : IDisposable, IAsyncDisposable
{
    ProtocolSessionState State { get; }
    ProtocolCapabilities Capabilities { get; }

    void Focus();

    ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default);
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);
    ValueTask CloseAsync(CancellationToken cancellationToken = default);

}
