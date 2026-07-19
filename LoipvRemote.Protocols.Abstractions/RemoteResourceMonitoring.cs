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

public sealed record RemoteDiskSnapshot(string Name, long UsedBytes, long TotalBytes)
{
    public double Percent => TotalBytes == 0
        ? 0d
        : Math.Clamp(UsedBytes * 100d / TotalBytes, 0d, 100d);
}

/// <summary>Display-ready resource values collected through a protocol-specific side channel.</summary>
public sealed record RemoteResourceSnapshot
{
    public RemoteResourceSnapshot(
        double? CpuPercent,
        long MemoryUsedBytes,
        long MemoryTotalBytes,
        double DiskPercent,
        long DiskUsedBytes,
        long DiskTotalBytes,
        long? ReceiveBytesPerSecond,
        long? TransmitBytesPerSecond,
        TimeSpan Uptime,
        IReadOnlyList<RemoteDiskSnapshot>? Disks = null)
    {
        this.CpuPercent = CpuPercent;
        this.MemoryUsedBytes = MemoryUsedBytes;
        this.MemoryTotalBytes = MemoryTotalBytes;
        this.DiskPercent = DiskPercent;
        this.DiskUsedBytes = DiskUsedBytes;
        this.DiskTotalBytes = DiskTotalBytes;
        this.ReceiveBytesPerSecond = ReceiveBytesPerSecond;
        this.TransmitBytesPerSecond = TransmitBytesPerSecond;
        this.Uptime = Uptime;
        this.Disks = Disks ?? [new RemoteDiskSnapshot("Disk", DiskUsedBytes, DiskTotalBytes)];
    }

    public double? CpuPercent { get; }
    public long MemoryUsedBytes { get; }
    public long MemoryTotalBytes { get; }
    public double DiskPercent { get; }
    public long DiskUsedBytes { get; }
    public long DiskTotalBytes { get; }
    public long? ReceiveBytesPerSecond { get; }
    public long? TransmitBytesPerSecond { get; }
    public TimeSpan Uptime { get; }
    public IReadOnlyList<RemoteDiskSnapshot> Disks { get; }
}

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
