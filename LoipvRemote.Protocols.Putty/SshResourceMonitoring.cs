using System.Globalization;

namespace LoipvRemote.Protocols.Putty;

/// <summary>Raw counters sampled from a Linux host through the dedicated SSH monitor channel.</summary>
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

/// <summary>Display-ready values for the SSH resource bar.</summary>
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
            ? 0d
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

public static class LinuxResourceSampleParser
{
    private static readonly string[] RequiredKeys =
    [
        "cpu_total", "cpu_idle", "mem_total", "mem_available", "disk_total",
        "disk_used", "net_rx", "net_tx", "uptime_seconds"
    ];

    public static LinuxResourceSample Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            throw new FormatException("The resource probe returned no data.");

        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1)
                continue;

            string key = line[..separator];
            if (Array.IndexOf(RequiredKeys, key) < 0)
                continue;

            string value = line[(separator + 1)..];
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) || parsed < 0)
                throw new FormatException($"The resource probe returned an invalid value for {key}.");

            values[key] = parsed;
        }

        foreach (string key in RequiredKeys)
        {
            if (!values.ContainsKey(key))
                throw new FormatException($"The resource probe did not return {key}.");
        }

        if (values["cpu_idle"] > values["cpu_total"] ||
            values["mem_available"] > values["mem_total"] ||
            values["disk_used"] > values["disk_total"])
        {
            throw new FormatException("The resource probe returned inconsistent counters.");
        }

        return new LinuxResourceSample(
            values["cpu_total"],
            values["cpu_idle"],
            values["mem_total"],
            values["mem_total"] - values["mem_available"],
            values["disk_total"],
            values["disk_used"],
            values["net_rx"],
            values["net_tx"],
            TimeSpan.FromSeconds(values["uptime_seconds"]));
    }
}
