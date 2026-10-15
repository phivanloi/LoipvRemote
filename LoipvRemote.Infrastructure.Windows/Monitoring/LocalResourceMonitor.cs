using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Infrastructure.Windows.Monitoring;

/// <summary>Collects resource usage for the Windows machine running Loipv Remote.</summary>
public sealed class LocalResourceMonitor : IRemoteResourceMonitor
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private readonly object _sync = new();
    private CancellationTokenSource? _cancellation;
    private bool _isActive;
    private bool _disposed;
    private CpuTimes? _previousCpu;
    private NetworkSample? _previousNetwork;
    private RemoteResourceSnapshot? _lastSnapshot;
    private RemoteResourceMonitorStatus _lastStatus =
        new(RemoteResourceMonitorState.WaitingForActiveTab, "Local resource monitoring paused");

    public event Action<RemoteResourceSnapshot>? SnapshotUpdated;
    public event Action<RemoteResourceMonitorStatus>? StatusChanged;

    public RemoteResourceSnapshot? LastSnapshot
    {
        get { lock (_sync) return _lastSnapshot; }
    }

    public RemoteResourceMonitorStatus LastStatus
    {
        get { lock (_sync) return _lastStatus; }
    }

    public void Start()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_cancellation is not null)
                return;
            _isActive = true;
            _cancellation = new CancellationTokenSource();
            _ = Task.Run(() => RunAsync(_cancellation.Token));
        }
    }

    public void SetIsActive(bool isActive)
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _isActive = isActive;
        }
        if (!isActive)
            PublishStatus(new(RemoteResourceMonitorState.WaitingForActiveTab, "Local resource monitoring paused"));
    }

    public void StopMonitoring()
    {
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            cancellation = _cancellation;
            _cancellation = null;
            _isActive = false;
            _previousCpu = null;
            _previousNetwork = null;
        }
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
        }
        StopMonitoring();
        GC.SuppressFinalize(this);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!IsActive())
            {
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                RemoteResourceSnapshot snapshot = Collect();
                PublishSnapshot(snapshot);
                PublishStatus(new(RemoteResourceMonitorState.Monitoring, "Monitoring resources on this computer"));
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                PublishStatus(new(RemoteResourceMonitorState.Unavailable, "Local resource monitoring is unavailable"));
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private RemoteResourceSnapshot Collect()
    {
        CpuTimes currentCpu = ReadCpuTimes();
        NetworkSample currentNetwork = ReadNetworkSample();
        CpuTimes? previousCpu;
        NetworkSample? previousNetwork;
        lock (_sync)
        {
            previousCpu = _previousCpu;
            previousNetwork = _previousNetwork;
            _previousCpu = currentCpu;
            _previousNetwork = currentNetwork;
        }

        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref memory))
            throw new InvalidOperationException("GlobalMemoryStatusEx failed.");

        RemoteDiskSnapshot[] disks = DriveInfo.GetDrives()
            .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
            .Select(drive => new RemoteDiskSnapshot(
                drive.Name.TrimEnd(Path.DirectorySeparatorChar),
                drive.TotalSize - drive.AvailableFreeSpace,
                drive.TotalSize))
            .ToArray();
        long diskTotal = disks.Sum(disk => disk.TotalBytes);
        long diskUsed = disks.Sum(disk => disk.UsedBytes);

        return new RemoteResourceSnapshot(
            CalculateCpuPercent(previousCpu, currentCpu),
            checked((long)(memory.TotalPhysical - memory.AvailablePhysical)),
            checked((long)memory.TotalPhysical),
            diskTotal == 0 ? 0 : diskUsed * 100d / diskTotal,
            diskUsed,
            diskTotal,
            CalculateNetworkRate(previousNetwork, currentNetwork, static sample => sample.ReceivedBytes),
            CalculateNetworkRate(previousNetwork, currentNetwork, static sample => sample.SentBytes),
            TimeSpan.FromMilliseconds(Environment.TickCount64),
            disks);
    }

    internal static double? CalculateCpuPercent(CpuTimes? previous, CpuTimes current)
    {
        if (previous is null)
            return null;
        ulong idle = current.Idle - previous.Value.Idle;
        ulong kernel = current.Kernel - previous.Value.Kernel;
        ulong user = current.User - previous.Value.User;
        ulong total = kernel + user;
        return total == 0 ? null : Math.Clamp((total - Math.Min(idle, total)) * 100d / total, 0d, 100d);
    }

    internal static long? CalculateNetworkRate(
        NetworkSample? previous,
        NetworkSample current,
        Func<NetworkSample, long> selector)
    {
        if (previous is null)
            return null;
        double seconds = (current.Timestamp - previous.Value.Timestamp) / (double)Stopwatch.Frequency;
        if (seconds <= 0)
            return null;
        return Math.Max(0, (long)((selector(current) - selector(previous.Value)) / seconds));
    }

    private static CpuTimes ReadCpuTimes()
    {
        if (!GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user))
            throw new InvalidOperationException("GetSystemTimes failed.");
        return new CpuTimes(idle.Value, kernel.Value, user.Value);
    }

    private static NetworkSample ReadNetworkSample()
    {
        long received = 0;
        long sent = 0;
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up ||
                adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            IPv4InterfaceStatistics statistics = adapter.GetIPv4Statistics();
            received += statistics.BytesReceived;
            sent += statistics.BytesSent;
        }
        return new NetworkSample(Stopwatch.GetTimestamp(), received, sent);
    }

    private bool IsActive()
    {
        lock (_sync) return _isActive;
    }

    private void PublishSnapshot(RemoteResourceSnapshot snapshot)
    {
        lock (_sync) _lastSnapshot = snapshot;
        SnapshotUpdated?.Invoke(snapshot);
    }

    private void PublishStatus(RemoteResourceMonitorStatus status)
    {
        bool changed;
        lock (_sync)
        {
            changed = _lastStatus != status;
            _lastStatus = status;
        }
        if (changed)
            StatusChanged?.Invoke(status);
    }

    internal readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);
    internal readonly record struct NetworkSample(long Timestamp, long ReceivedBytes, long SentBytes);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);
}
