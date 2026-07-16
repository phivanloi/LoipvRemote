using System;

namespace LoipvRemote.Protocols.Putty.Monitoring;

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

public static class RemoteResourceSnapshotCalculator
{
    public static RemoteResourceSnapshot Calculate(
        LinuxResourceSample current,
        LinuxResourceSample? previous,
        TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(current);

        double? cpuPercent = null;
        long? receiveRate = null;
        long? transmitRate = null;

        if (previous is not null)
        {
            long totalDelta = current.CpuTotalTicks - previous.CpuTotalTicks;
            long idleDelta = current.CpuIdleTicks - previous.CpuIdleTicks;
            if (totalDelta > 0 && idleDelta >= 0 && idleDelta <= totalDelta)
                cpuPercent = Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0d, 100d);

            if (elapsed > TimeSpan.Zero &&
                current.ReceiveBytes >= previous.ReceiveBytes &&
                current.TransmitBytes >= previous.TransmitBytes)
            {
                receiveRate = (long)Math.Round((current.ReceiveBytes - previous.ReceiveBytes) / elapsed.TotalSeconds);
                transmitRate = (long)Math.Round((current.TransmitBytes - previous.TransmitBytes) / elapsed.TotalSeconds);
            }
        }

        double diskPercent = current.DiskTotalBytes == 0
            ? 0
            : Math.Clamp(current.DiskUsedBytes * 100d / current.DiskTotalBytes, 0d, 100d);

        return new RemoteResourceSnapshot(
            cpuPercent,
            current.MemoryUsedBytes,
            current.MemoryTotalBytes,
            diskPercent,
            current.DiskUsedBytes,
            current.DiskTotalBytes,
            receiveRate,
            transmitRate,
            current.Uptime);
    }
}
