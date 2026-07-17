using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.WinUI.Sessions;

public sealed class RemoteSessionTab(ConnectionDefinition connection)
{
    private CancellationTokenSource? _connectionCancellation;

    public ConnectionDefinition Connection { get; } = connection ?? throw new ArgumentNullException(nameof(connection));
    public IProtocolSession? Session { get; private set; }
    public RemoteSessionTabState State { get; private set; } = RemoteSessionTabState.Created;

    internal SemaphoreSlim LifecycleGate { get; } = new(1, 1);

    internal CancellationToken BeginConnecting(IProtocolSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        _connectionCancellation?.Dispose();
        _connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Session = session;
        State = RemoteSessionTabState.Connecting;
        return _connectionCancellation.Token;
    }

    internal void CancelPendingConnection() => _connectionCancellation?.Cancel();

    internal void MarkConnected(IProtocolSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        State = RemoteSessionTabState.Connected;
        ClearConnectionCancellation();
    }

    internal void MarkFaulted(IProtocolSession session)
    {
        if (ReferenceEquals(Session, session))
            Session = null;
        State = RemoteSessionTabState.Faulted;
        ClearConnectionCancellation();
    }

    internal void MarkClosed()
    {
        CancelPendingConnection();
        ClearConnectionCancellation();
        Session = null;
        State = RemoteSessionTabState.Closed;
    }

    private void ClearConnectionCancellation()
    {
        _connectionCancellation?.Dispose();
        _connectionCancellation = null;
    }
}

public enum RemoteSessionTabState
{
    Created,
    Connecting,
    Connected,
    Faulted,
    Closed
}
