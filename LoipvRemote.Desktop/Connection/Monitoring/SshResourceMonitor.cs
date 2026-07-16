using LoipvRemote.Protocols.Putty.Monitoring;

namespace LoipvRemote.Connection.Monitoring;

/// <summary>Desktop adapter for the protocol-owned SSH metrics monitor.</summary>
public sealed class SshResourceMonitor : IDisposable
{
    private readonly PuttyResourceMonitor _inner;

    public event Action<RemoteResourceSnapshot>? SnapshotUpdated;
    public event Action<RemoteResourceMonitorStatus>? StatusChanged;

    public SshResourceMonitor(Connection.ConnectionInfo connection, IPuttyHostKeyTrustStore hostKeyTrustStore)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _inner = new PuttyResourceMonitor(
            new PuttyMonitoringConnection(connection.Hostname, connection.Port, connection.Username, connection.Password, connection.Name),
            hostKeyTrustStore ?? throw new ArgumentNullException(nameof(hostKeyTrustStore)));
        _inner.SnapshotUpdated += snapshot => SnapshotUpdated?.Invoke(snapshot);
        _inner.StatusChanged += status => StatusChanged?.Invoke(new((RemoteResourceMonitorState)status.State, status.Message, status.Fingerprint));
    }

    public void Start() => _inner.Start();
    public void SetIsActive(bool isActive) => _inner.SetIsActive(isActive);
    public void Stop() => _inner.Stop();
    public void Dispose() => _inner.Dispose();
}

public enum RemoteResourceMonitorState
{
    WaitingForActiveTab,
    Connecting,
    Monitoring,
    AuthenticationUnavailable,
    Unavailable
}

public sealed record RemoteResourceMonitorStatus(RemoteResourceMonitorState State, string Message, string? Fingerprint = null);
