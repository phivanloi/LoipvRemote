using System;

namespace LoipvRemote.Protocols.Putty.Monitoring;

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
