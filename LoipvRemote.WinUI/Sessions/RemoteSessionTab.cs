using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.WinUI.Sessions;

public sealed class RemoteSessionTab(ConnectionDefinition connection)
{
    private CancellationTokenSource? _connectionCancellation;
    private CancellationTokenSource? _reconnectCancellation;

    /// <summary>Phiên đang trong quá trình khôi phục kết nối SSH.</summary>
    internal bool IsReconnecting => _reconnectCancellation is not null;

    internal void BeginReconnect(CancellationTokenSource cancellation)
    {
        // The reconnect operation owns/disposes this source. The tab borrows
        // it only so closing/switching can cancel both backoff and TCP probing.
        _reconnectCancellation = cancellation;
    }

    internal void EndReconnect()
    {
        _reconnectCancellation = null;
    }

    public ConnectionDefinition Connection { get; } = connection ?? throw new ArgumentNullException(nameof(connection));
    public IProtocolSession? Session { get; private set; }
    public IRemoteResourceMonitor? ResourceMonitor { get; private set; }
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

    internal void CancelPendingConnection()
    {
        _connectionCancellation?.Cancel();
        _reconnectCancellation?.Cancel();
    }

    internal void MarkConnected(IProtocolSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        State = RemoteSessionTabState.Connected;
        ClearConnectionCancellation();
    }

    internal void SetResourceMonitor(IRemoteResourceMonitor? resourceMonitor)
    {
        if (ReferenceEquals(ResourceMonitor, resourceMonitor))
            return;

        ResourceMonitor?.Dispose();
        ResourceMonitor = resourceMonitor;
    }

    /// <summary>Tách monitoring khỏi UI trước khi dọn kết nối mạng trên worker.</summary>
    internal IRemoteResourceMonitor? DetachResourceMonitor()
    {
        var monitor = ResourceMonitor;
        ResourceMonitor = null;
        return monitor;
    }

    internal void SetResourceMonitoringActive(bool isActive) => ResourceMonitor?.SetIsActive(isActive);

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
        EndReconnect();
        Session = null;
        ResourceMonitor?.Dispose();
        ResourceMonitor = null;
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
