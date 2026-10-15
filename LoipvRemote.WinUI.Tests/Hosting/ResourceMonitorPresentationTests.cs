using LoipvRemote.Protocols.Abstractions;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class ResourceMonitorPresentationTests
{
    private static readonly RemoteResourceSnapshot Snapshot = new(
        CpuPercent: 81,
        MemoryUsedBytes: 6L * 1024 * 1024 * 1024,
        MemoryTotalBytes: 8L * 1024 * 1024 * 1024,
        DiskPercent: 60,
        DiskUsedBytes: 180L * 1024 * 1024 * 1024,
        DiskTotalBytes: 300L * 1024 * 1024 * 1024,
        ReceiveBytesPerSecond: 1,
        TransmitBytesPerSecond: 2,
        Uptime: TimeSpan.FromHours(1),
        Disks:
        [
            new RemoteDiskSnapshot("C:", 80L * 1024 * 1024 * 1024, 100L * 1024 * 1024 * 1024),
            new RemoteDiskSnapshot("D:", 100L * 1024 * 1024 * 1024, 200L * 1024 * 1024 * 1024)
        ]);

    [Test]
    public void CompactValuesContainOnlyAggregatePercentages()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ResourceMonitorPresentation.FormatMemoryPercent(Snapshot), Is.EqualTo("75 %"));
            Assert.That(ResourceMonitorPresentation.FormatDiskPercent(Snapshot), Is.EqualTo("60 %"));
        });
    }

    [Test]
    public void DetailTextIncludesMemoryTotalsAndEveryDisk()
    {
        string memory = ResourceMonitorPresentation.FormatMemoryDetails(Snapshot);
        string disks = ResourceMonitorPresentation.FormatDiskDetails(Snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(memory, Does.Contain("6.0 GB / 8.0 GB"));
            Assert.That(disks, Does.Contain("2 disks"));
            Assert.That(disks, Does.Contain("C: 80.0 GB / 100.0 GB (80 %)"));
            Assert.That(disks, Does.Contain("D: 100.0 GB / 200.0 GB (50 %)"));
            Assert.That(disks, Does.Contain("Total: 180.0 GB / 300.0 GB (60 %)"));
        });
    }

    [TestCase(80, false)]
    [TestCase(80.01, true)]
    [TestCase(100, true)]
    [TestCase(null, false)]
    public void WarningStartsOnlyAboveEightyPercent(double? percent, bool expected)
    {
        Assert.That(ResourceMonitorPresentation.IsWarning(percent), Is.EqualTo(expected));
    }
}
