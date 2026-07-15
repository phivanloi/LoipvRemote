using System;

namespace LoipvRemote.Connection.Monitoring
{
    public sealed record LinuxResourceSample(
        long CpuTotalTicks,
        long CpuIdleTicks,
        long MemoryTotalBytes,
        long MemoryUsedBytes,
        long DiskTotalBytes,
        long DiskUsedBytes,
        long ReceiveBytes,
        long TransmitBytes,
        TimeSpan Uptime);
}
