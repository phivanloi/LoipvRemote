namespace LoipvRemote.Protocols.Abstractions;

public enum RemoteResourceMonitorState
{
    WaitingForActiveTab,
    Connecting,
    Monitoring,
    AuthenticationUnavailable,
    Unavailable
}

public sealed record RemoteResourceMonitorStatus(RemoteResourceMonitorState State, string Message);

/// <summary>Display-ready resource values collected through a protocol-specific side channel.</summary>
public sealed record RemoteResourceSnapshot(
    double? CpuPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    double DiskPercent,
    long DiskUsedBytes,
    long DiskTotalBytes,
    long? ReceiveBytesPerSecond,
    long? TransmitBytesPerSecond,
    TimeSpan Uptime);

/// <summary>Lifecycle contract shared by SSH and RDP resource monitors.</summary>
public interface IRemoteResourceMonitor : IDisposable
{
    event Action<RemoteResourceSnapshot>? SnapshotUpdated;
    event Action<RemoteResourceMonitorStatus>? StatusChanged;

    RemoteResourceSnapshot? LastSnapshot { get; }
    RemoteResourceMonitorStatus LastStatus { get; }

    void Start();
    void SetIsActive(bool isActive);
    void StopMonitoring();
}
