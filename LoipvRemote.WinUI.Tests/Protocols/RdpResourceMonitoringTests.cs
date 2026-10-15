using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Rdp;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Protocols;

public sealed class RdpResourceMonitoringTests
{
    [Test]
    public void WindowsProbePrefersDcomForIpTargetsAndKeepsWsManFallback()
    {
        string sourceRoot = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.Protocols.Rdp"));
        string source = File.ReadAllText(Path.Combine(sourceRoot, "RdpResourceMonitoring.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("New-CimSessionOption -Protocol Dcom"));
            Assert.That(source, Does.Contain("-SessionOption $dcomOption"));
            Assert.That(source, Does.Contain("[System.Net.IPAddress]::TryParse"));
            Assert.That(source, Does.Contain("TimeSpan.FromSeconds(45)"));
            Assert.That(source, Does.Contain("startInfo.Environment.Remove(\"PSModulePath\")"));
            Assert.That(source, Does.Contain("LOIPVREMOTE_MONITOR_QUERY"));
            Assert.That(source, Does.Not.Contain("ConvertTo-SecureString $password"));
            Assert.That(source, Does.Not.Contain("Remove-CimSession"));
            Assert.That(source, Does.Contain("Windows resource monitoring is unavailable"));
        });
    }

    [Test]
    public void WindowsPerformanceProbeUsesClassicWmiFallbackWithEnoughTimeForRemoteCounters()
    {
        string sourceRoot = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.Protocols.Rdp"));
        string source = File.ReadAllText(Path.Combine(sourceRoot, "RdpResourceMonitoring.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("OptionalCounterTimeout = TimeSpan.FromSeconds(20)"));
            Assert.That(source, Does.Contain("System.Management.ManagementScope"));
            Assert.That(source, Does.Contain("System.Management.ManagementObjectSearcher"));
            Assert.That(source, Does.Contain("Win32_PerfFormattedData_PerfOS_Processor"));
            Assert.That(source, Does.Contain("Win32_PerfFormattedData_Tcpip_NetworkInterface"));
        });
    }

    [Test]
    public void WindowsResourceSampleParserKeepsCoreMetricsWhenPerformanceCountersAreUnavailable()
    {
        const string json = """
            {"memoryUsedBytes":6442450944,"memoryTotalBytes":8589934592,"diskUsedBytes":64424509440,"diskTotalBytes":161061273600,"disks":[{"name":"C:","usedBytes":64424509440,"totalBytes":161061273600}],"uptimeSeconds":86461}
            """;

        RemoteResourceSnapshot snapshot = WindowsResourceSampleParser.Parse(json);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.CpuPercent, Is.Null);
            Assert.That(snapshot.MemoryUsedBytes, Is.EqualTo(6_442_450_944));
            Assert.That(snapshot.MemoryTotalBytes, Is.EqualTo(8_589_934_592));
            Assert.That(snapshot.DiskPercent, Is.EqualTo(40));
            Assert.That(snapshot.Disks, Has.Count.EqualTo(1));
            Assert.That(snapshot.ReceiveBytesPerSecond, Is.Null);
            Assert.That(snapshot.TransmitBytesPerSecond, Is.Null);
            Assert.That(snapshot.Uptime, Is.EqualTo(TimeSpan.FromSeconds(86_461)));
        });
    }

    [Test]
    public void WindowsResourceSampleParserParsesAllMetrics()
    {
        const string json = """
            {"cpuPercent":37.5,"memoryUsedBytes":6442450944,"memoryTotalBytes":8589934592,"diskUsedBytes":64424509440,"diskTotalBytes":161061273600,"disks":[{"name":"C:","usedBytes":42949672960,"totalBytes":107374182400},{"name":"D:","usedBytes":21474836480,"totalBytes":53687091200}],"receiveBytesPerSecond":12345,"transmitBytesPerSecond":6789,"uptimeSeconds":86461}
            """;

        RemoteResourceSnapshot snapshot = WindowsResourceSampleParser.Parse(json);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.CpuPercent, Is.EqualTo(37.5));
            Assert.That(snapshot.MemoryUsedBytes, Is.EqualTo(6_442_450_944));
            Assert.That(snapshot.DiskPercent, Is.EqualTo(40));
            Assert.That(snapshot.Disks, Has.Count.EqualTo(2));
            Assert.That(snapshot.Disks[0].Name, Is.EqualTo("C:"));
            Assert.That(snapshot.Disks[1].Percent, Is.EqualTo(40));
            Assert.That(snapshot.ReceiveBytesPerSecond, Is.EqualTo(12_345));
            Assert.That(snapshot.TransmitBytesPerSecond, Is.EqualTo(6_789));
            Assert.That(snapshot.Uptime, Is.EqualTo(TimeSpan.FromSeconds(86_461)));
        });
    }

    [TestCase("{\"cpuPercent\":101,\"memoryUsedBytes\":1,\"memoryTotalBytes\":1,\"diskUsedBytes\":1,\"diskTotalBytes\":1,\"receiveBytesPerSecond\":0,\"transmitBytesPerSecond\":0,\"uptimeSeconds\":1}")]
    [TestCase("{\"cpuPercent\":1,\"memoryUsedBytes\":2,\"memoryTotalBytes\":1,\"diskUsedBytes\":1,\"diskTotalBytes\":1,\"receiveBytesPerSecond\":0,\"transmitBytesPerSecond\":0,\"uptimeSeconds\":1}")]
    public void WindowsResourceSampleParserRejectsInconsistentMetrics(string json)
    {
        Assert.Throws<FormatException>(() => WindowsResourceSampleParser.Parse(json));
    }

    [Test]
    public async Task RdpMonitorPublishesWindowsSnapshotWhileActive()
    {
        var expected = new RemoteResourceSnapshot(25, 4, 8, 50, 50, 100, 12, 34, TimeSpan.FromHours(2));
        using var monitor = new RdpResourceMonitor(new StubWindowsResourceCollector(expected), TimeSpan.FromMilliseconds(5));
        var published = new TaskCompletionSource<RemoteResourceSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.SnapshotUpdated += snapshot => published.TrySetResult(snapshot);

        monitor.Start();
        RemoteResourceSnapshot actual = await published.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(monitor.LastStatus.State, Is.EqualTo(RemoteResourceMonitorState.Monitoring));
        });
    }

    [Test]
    public async Task RdpMonitorKeepsTheLastPerformanceSampleAcrossOneTransientCounterMiss()
    {
        var full = new RemoteResourceSnapshot(25, 4, 8, 50, 50, 100, 12, 34, TimeSpan.FromHours(2));
        var partial = new RemoteResourceSnapshot(null, 5, 8, 51, 51, 100, null, null, TimeSpan.FromHours(2));
        using var monitor = new RdpResourceMonitor(
            new SequenceWindowsResourceCollector(full, partial),
            TimeSpan.FromMilliseconds(5));
        var second = new TaskCompletionSource<RemoteResourceSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        int updates = 0;
        monitor.SnapshotUpdated += snapshot =>
        {
            if (Interlocked.Increment(ref updates) == 2)
                second.TrySetResult(snapshot);
        };

        monitor.Start();
        RemoteResourceSnapshot actual = await second.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(actual.CpuPercent, Is.EqualTo(25));
            Assert.That(actual.ReceiveBytesPerSecond, Is.EqualTo(12));
            Assert.That(actual.TransmitBytesPerSecond, Is.EqualTo(34));
            Assert.That(actual.MemoryUsedBytes, Is.EqualTo(5));
            Assert.That(actual.DiskUsedBytes, Is.EqualTo(51));
        });
    }

    [Test]
    public async Task RdpMonitorKeepsTheLastSnapshotAcrossOneTransientCollectorFailure()
    {
        var full = new RemoteResourceSnapshot(25, 4, 8, 50, 50, 100, 12, 34, TimeSpan.FromHours(2));
        using var monitor = new RdpResourceMonitor(
            new SuccessThenFailWindowsResourceCollector(full),
            TimeSpan.FromMilliseconds(5));
        var handledFailure = new TaskCompletionSource<RemoteResourceMonitorStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.StatusChanged += status =>
        {
            if (status.State == RemoteResourceMonitorState.Unavailable ||
                status.Message.Contains("retaining", StringComparison.OrdinalIgnoreCase))
            {
                handledFailure.TrySetResult(status);
            }
        };

        monitor.Start();
        RemoteResourceMonitorStatus status = await handledFailure.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(status.State, Is.EqualTo(RemoteResourceMonitorState.Monitoring));
            Assert.That(status.Message, Does.Contain("retaining"));
            Assert.That(monitor.LastSnapshot, Is.EqualTo(full));
        });
    }

    [Test]
    public async Task RdpMonitorReportsUnavailableWhenWindowsProbeFails()
    {
        using var monitor = new RdpResourceMonitor(new FailingWindowsResourceCollector(), TimeSpan.FromMilliseconds(5));
        var unavailable = new TaskCompletionSource<RemoteResourceMonitorStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.StatusChanged += status =>
        {
            if (status.State == RemoteResourceMonitorState.Unavailable)
                unavailable.TrySetResult(status);
        };

        monitor.Start();
        RemoteResourceMonitorStatus status = await unavailable.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(status.Message, Does.Contain("Windows resource monitoring"));
            Assert.That(monitor.LastSnapshot, Is.Null);
        });
    }

    [Test]
    public void FactoryUsesDomainQualifiedRdpCredentials()
    {
        var options = new ConnectionNodeOptions(
            new Dictionary<string, string> { ["Username"] = "administrator", ["Domain"] = "CONTOSO" },
            []);
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "Server", "server.contoso.test", 3389, ProtocolKind.Rdp,
            CredentialReference.None, Options: options);
        RdpResourceMonitorConnection? captured = null;
        var factory = new RdpResourceMonitorFactory(
            _ => "secret",
            connection =>
            {
                captured = connection;
                return new StubWindowsResourceCollector(new RemoteResourceSnapshot(null, 0, 0, 0, 0, 0, null, null, TimeSpan.Zero));
            });

        IRemoteResourceMonitor? monitor = factory.Create(definition);

        Assert.Multiple(() =>
        {
            Assert.That(monitor, Is.Not.Null);
            Assert.That(captured?.Host, Is.EqualTo("server.contoso.test"));
            Assert.That(captured?.Username, Is.EqualTo("CONTOSO\\administrator"));
            Assert.That(captured?.Password, Is.EqualTo("secret"));
        });
        monitor?.Dispose();
    }

    private sealed class StubWindowsResourceCollector(RemoteResourceSnapshot snapshot) : IWindowsResourceCollector
    {
        public Task<RemoteResourceSnapshot> CollectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FailingWindowsResourceCollector : IWindowsResourceCollector
    {
        public Task<RemoteResourceSnapshot> CollectAsync(CancellationToken cancellationToken) =>
            Task.FromException<RemoteResourceSnapshot>(new InvalidOperationException("WinRM unavailable"));

        public void Dispose()
        {
        }
    }

    private sealed class SequenceWindowsResourceCollector(params RemoteResourceSnapshot[] snapshots) : IWindowsResourceCollector
    {
        private int _index;

        public Task<RemoteResourceSnapshot> CollectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = Math.Min(Interlocked.Increment(ref _index) - 1, snapshots.Length - 1);
            return Task.FromResult(snapshots[index]);
        }

        public void Dispose()
        {
        }
    }

    private sealed class SuccessThenFailWindowsResourceCollector(RemoteResourceSnapshot snapshot) : IWindowsResourceCollector
    {
        private int _calls;

        public Task<RemoteResourceSnapshot> CollectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Interlocked.Increment(ref _calls) == 1
                ? Task.FromResult(snapshot)
                : Task.FromException<RemoteResourceSnapshot>(new InvalidOperationException("Transient DCOM failure"));
        }

        public void Dispose()
        {
        }
    }
}
