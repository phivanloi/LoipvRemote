using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Putty;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Protocols;

public sealed class SshResourceMonitoringTests
{
    [Test]
    public void LinuxResourceSampleParserIgnoresTerminalNoiseAndParsesAllMetrics()
    {
        const string output = "Welcome to host\n" +
                              "cpu_total=500\n" +
                              "cpu_idle=150\n" +
                              "mem_total=8589934592\n" +
                              "mem_available=4294967296\n" +
                              "disk_total=107374182400\n" +
                              "disk_used=42949672960\n" +
                              "net_rx=12345\n" +
                              "net_tx=6789\n" +
                              "uptime_seconds=86461\n";

        LinuxResourceSample sample = LinuxResourceSampleParser.Parse(output);

        Assert.Multiple(() =>
        {
            Assert.That(sample.MemoryUsedBytes, Is.EqualTo(4_294_967_296));
            Assert.That(sample.DiskUsedBytes, Is.EqualTo(42_949_672_960));
            Assert.That(sample.ReceiveBytes, Is.EqualTo(12_345));
            Assert.That(sample.TransmitBytes, Is.EqualTo(6_789));
            Assert.That(sample.Uptime, Is.EqualTo(TimeSpan.FromSeconds(86_461)));
        });
    }

    [Test]
    public void SnapshotCalculatorProducesCpuDiskAndAggregateNetworkRates()
    {
        var previous = new LinuxResourceSample(100, 40, 8, 4, 100, 50, 1_000, 2_000, TimeSpan.FromSeconds(10));
        var current = new LinuxResourceSample(200, 90, 8, 3, 100, 60, 3_000, 5_000, TimeSpan.FromSeconds(15));

        RemoteResourceSnapshot snapshot = RemoteResourceSnapshotCalculator.Calculate(current, previous, TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.CpuPercent, Is.EqualTo(50));
            Assert.That(snapshot.DiskPercent, Is.EqualTo(60));
            Assert.That(snapshot.ReceiveBytesPerSecond, Is.EqualTo(400));
            Assert.That(snapshot.TransmitBytesPerSecond, Is.EqualTo(600));
            Assert.That(snapshot.Uptime, Is.EqualTo(TimeSpan.FromSeconds(15)));
        });
    }

    [Test]
    public void LinuxResourceSampleParserReturnsEveryReportedFileSystem()
    {
        const string output = "cpu_total=500\n" +
                              "cpu_idle=150\n" +
                              "mem_total=8589934592\n" +
                              "mem_available=4294967296\n" +
                              "disk_total=429496729600\n" +
                              "disk_used=193273528320\n" +
                              "disk=/\t107374182400\t64424509440\n" +
                              "disk=/data\t322122547200\t128849018880\n" +
                              "net_rx=12345\n" +
                              "net_tx=6789\n" +
                              "uptime_seconds=86461\n";

        LinuxResourceSample sample = LinuxResourceSampleParser.Parse(output);
        RemoteResourceSnapshot snapshot = RemoteResourceSnapshotCalculator.Calculate(sample, null, TimeSpan.Zero);

        Assert.Multiple(() =>
        {
            Assert.That(sample.Disks, Has.Count.EqualTo(2));
            Assert.That(snapshot.Disks, Has.Count.EqualTo(2));
            Assert.That(snapshot.Disks[0].Name, Is.EqualTo("/"));
            Assert.That(snapshot.Disks[0].Percent, Is.EqualTo(60));
            Assert.That(snapshot.Disks[1].Name, Is.EqualTo("/data"));
            Assert.That(snapshot.DiskPercent, Is.EqualTo(45));
        });
    }

    [Test]
    public void LinuxResourceSampleParserParsesPortableDfTableAndAggregatesDisks()
    {
        const string output = "cpu_total=500\n" +
                              "cpu_idle=150\n" +
                              "mem_total=8589934592\n" +
                              "mem_available=4294967296\n" +
                              "disk_total=100\n" +
                              "disk_used=60\n" +
                              "disk_table_begin\n" +
                              "Filesystem 1024-blocks Used Available Capacity Mounted on\n" +
                              "/dev/sda1 104857600 62914560 41943040 60% /\n" +
                              "/dev/sdb1 314572800 125829120 188743680 40% /data\n" +
                              "disk_table_end\n" +
                              "net_rx=12345\n" +
                              "net_tx=6789\n" +
                              "uptime_seconds=86461\n";

        LinuxResourceSample sample = LinuxResourceSampleParser.Parse(output);

        Assert.Multiple(() =>
        {
            Assert.That(sample.Disks, Has.Count.EqualTo(2));
            Assert.That(sample.DiskTotalBytes, Is.EqualTo(400L * 1024 * 1024 * 1024));
            Assert.That(sample.DiskUsedBytes, Is.EqualTo(180L * 1024 * 1024 * 1024));
            Assert.That(sample.Disks[1].Name, Is.EqualTo("/data"));
        });
    }

    [Test]
    public async Task MonitorPublishesValuesAfterTheSecondActiveSample()
    {
        var collector = new SequenceCollector(
            new LinuxResourceSample(100, 40, 8, 4, 100, 50, 1_000, 2_000, TimeSpan.FromSeconds(10)),
            new LinuxResourceSample(200, 90, 8, 3, 100, 60, 3_000, 5_000, TimeSpan.FromSeconds(15)));
        using var monitor = new SshResourceMonitor(collector, TimeSpan.FromMilliseconds(5));
        var published = new TaskCompletionSource<RemoteResourceSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.SnapshotUpdated += snapshot =>
        {
            if (snapshot.CpuPercent is not null)
                published.TrySetResult(snapshot);
        };

        monitor.Start();
        RemoteResourceSnapshot snapshot = await published.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.CpuPercent, Is.EqualTo(50));
            Assert.That(snapshot.ReceiveBytesPerSecond, Is.GreaterThan(0));
            Assert.That(monitor.LastStatus.State, Is.EqualTo(RemoteResourceMonitorState.Monitoring));
        });
    }

    private sealed class SequenceCollector(params LinuxResourceSample[] samples) : ILinuxResourceCollector
    {
        private readonly Queue<LinuxResourceSample> _samples = new(samples);
        private LinuxResourceSample? _lastSample;

        public Task<LinuxResourceSample> CollectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _lastSample = _samples.Count > 0 ? _samples.Dequeue() : _lastSample!;
            return Task.FromResult(_lastSample);
        }

        public void Dispose() { }
    }
}
