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
            Assert.That(source, Does.Not.Contain("ConvertTo-SecureString $password"));
            Assert.That(source, Does.Contain("Windows resource monitoring is unavailable"));
        });
    }

    [Test]
    public void WindowsResourceSampleParserParsesAllMetrics()
    {
        const string json = """
            {"cpuPercent":37.5,"memoryUsedBytes":6442450944,"memoryTotalBytes":8589934592,"diskUsedBytes":42949672960,"diskTotalBytes":107374182400,"receiveBytesPerSecond":12345,"transmitBytesPerSecond":6789,"uptimeSeconds":86461}
            """;

        RemoteResourceSnapshot snapshot = WindowsResourceSampleParser.Parse(json);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.CpuPercent, Is.EqualTo(37.5));
            Assert.That(snapshot.MemoryUsedBytes, Is.EqualTo(6_442_450_944));
            Assert.That(snapshot.DiskPercent, Is.EqualTo(40));
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
}
