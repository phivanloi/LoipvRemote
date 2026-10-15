using System.Globalization;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.WinUI;

public static class ResourceMonitorPresentation
{
    public static double? GetMemoryPercent(RemoteResourceSnapshot? snapshot)
    {
        if (snapshot is null)
            return null;
        return snapshot.MemoryTotalBytes == 0
            ? 0d
            : Math.Clamp(snapshot.MemoryUsedBytes * 100d / snapshot.MemoryTotalBytes, 0d, 100d);
    }

    public static string FormatMemoryPercent(RemoteResourceSnapshot? snapshot) =>
        FormatPercent(GetMemoryPercent(snapshot));

    public static string FormatDiskPercent(RemoteResourceSnapshot? snapshot) =>
        FormatPercent(snapshot?.DiskPercent);

    public static string FormatMemoryDetails(RemoteResourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return $"RAM: {FormatBytes(snapshot.MemoryUsedBytes)} / {FormatBytes(snapshot.MemoryTotalBytes)} " +
               $"({FormatPercent(GetMemoryPercent(snapshot))})";
    }

    public static string FormatDiskDetails(RemoteResourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string count = snapshot.Disks.Count == 1 ? "1 disk" : $"{snapshot.Disks.Count} disks";
        IEnumerable<string> details = snapshot.Disks.Select(disk =>
            $"{disk.Name}{(disk.Name.EndsWith(':') ? " " : ": ")}" +
            $"{FormatBytes(disk.UsedBytes)} / {FormatBytes(disk.TotalBytes)} " +
            $"({FormatPercent(disk.Percent)})");
        return string.Join(
            Environment.NewLine,
            [
                count,
                .. details,
                $"Total: {FormatBytes(snapshot.DiskUsedBytes)} / {FormatBytes(snapshot.DiskTotalBytes)} " +
                $"({FormatPercent(snapshot.DiskPercent)})"
            ]);
    }

    public static bool IsWarning(double? percent) => percent > 80d;

    private static string FormatPercent(double? percent) =>
        percent is double value
            ? $"{value.ToString("0", CultureInfo.CurrentCulture)} %"
            : "--";

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(bytes, 0);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{value.ToString("0", CultureInfo.CurrentCulture)} {units[unit]}"
            : $"{value.ToString("0.0", CultureInfo.CurrentCulture)} {units[unit]}";
    }
}
