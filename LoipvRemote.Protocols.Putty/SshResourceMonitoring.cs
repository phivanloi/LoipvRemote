using System.Globalization;
using LoipvRemote.Protocols.Abstractions;

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
    TimeSpan Uptime,
    IReadOnlyList<LinuxDiskSample>? DiskSamples = null)
{
    public IReadOnlyList<LinuxDiskSample> Disks { get; } =
        DiskSamples ?? [new LinuxDiskSample("/", DiskUsedBytes, DiskTotalBytes)];
}

public sealed record LinuxDiskSample(string Name, long UsedBytes, long TotalBytes);

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
            current.Uptime,
            current.Disks
                .Select(disk => new RemoteDiskSnapshot(disk.Name, disk.UsedBytes, disk.TotalBytes))
                .ToArray());
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
        var disks = new List<LinuxDiskSample>();
        var diskFileSystems = new HashSet<string>(StringComparer.Ordinal);
        bool readingDiskTable = false;
        foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line == "disk_table_begin")
            {
                readingDiskTable = true;
                continue;
            }

            if (line == "disk_table_end")
            {
                readingDiskTable = false;
                continue;
            }

            if (readingDiskTable)
            {
                string[] fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length < 6 || fields[0] == "Filesystem")
                    continue;
                if (!long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out long totalKilobytes) ||
                    !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out long usedKilobytes) ||
                    totalKilobytes <= 0 || usedKilobytes < 0 || usedKilobytes > totalKilobytes ||
                    !diskFileSystems.Add(fields[0]))
                {
                    continue;
                }

                try
                {
                    disks.Add(new LinuxDiskSample(
                        string.Join(' ', fields[5..]),
                        checked(usedKilobytes * 1024),
                        checked(totalKilobytes * 1024)));
                }
                catch (OverflowException exception)
                {
                    throw new FormatException("The resource probe returned a disk size that is too large.", exception);
                }
                continue;
            }

            if (line.StartsWith("disk=", StringComparison.Ordinal))
            {
                string[] fields = line[5..].Split('\t');
                if (fields.Length != 3 || string.IsNullOrWhiteSpace(fields[0]) ||
                    !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out long total) ||
                    !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out long used) ||
                    total < 0 || used < 0 || used > total)
                {
                    throw new FormatException("The resource probe returned an invalid disk entry.");
                }

                disks.Add(new LinuxDiskSample(fields[0], used, total));
                continue;
            }

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

        long diskTotal = values["disk_total"];
        long diskUsed = values["disk_used"];
        if (disks.Count > 0)
        {
            try
            {
                diskTotal = disks.Sum(disk => disk.TotalBytes);
                diskUsed = disks.Sum(disk => disk.UsedBytes);
            }
            catch (OverflowException exception)
            {
                throw new FormatException("The resource probe returned aggregate disk sizes that are too large.", exception);
            }
        }

        return new LinuxResourceSample(
            values["cpu_total"],
            values["cpu_idle"],
            values["mem_total"],
            values["mem_total"] - values["mem_available"],
            diskTotal,
            diskUsed,
            values["net_rx"],
            values["net_tx"],
            TimeSpan.FromSeconds(values["uptime_seconds"]),
            disks.Count == 0
                ? null
                : disks
                    .GroupBy(disk => disk.Name, StringComparer.Ordinal)
                    .Select(group => group.Last())
                    .ToArray());
    }
}
