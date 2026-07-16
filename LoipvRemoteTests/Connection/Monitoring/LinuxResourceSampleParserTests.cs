using System;
using LoipvRemote.Protocols.Putty.Monitoring;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Monitoring
{
    [TestFixture]
    public sealed class LinuxResourceSampleParserTests
    {
        [Test]
        public void ParsesMachineReadableLinuxMetrics()
        {
            const string output = "cpu_total=500\ncpu_idle=150\nmem_total=8589934592\nmem_available=4294967296\ndisk_total=107374182400\ndisk_used=42949672960\nnet_rx=12345\nnet_tx=6789\nuptime_seconds=86461\n";

            LinuxResourceSample sample = LinuxResourceSampleParser.Parse(output);

            Assert.Multiple(() =>
            {
                Assert.That(sample.CpuTotalTicks, Is.EqualTo(500));
                Assert.That(sample.CpuIdleTicks, Is.EqualTo(150));
                Assert.That(sample.MemoryUsedBytes, Is.EqualTo(4294967296));
                Assert.That(sample.DiskUsedBytes, Is.EqualTo(42949672960));
                Assert.That(sample.Uptime, Is.EqualTo(TimeSpan.FromSeconds(86461)));
            });
        }

        [Test]
        public void IgnoresShellBannersAndUnrelatedLines()
        {
            const string output = "Welcome to the server\nnotice: maintenance window\ncpu_total=500\ncpu_idle=150\nmem_total=8589934592\nmem_available=4294967296\ndisk_total=107374182400\ndisk_used=42949672960\nnet_rx=12345\nnet_tx=6789\nuptime_seconds=86461\n";

            LinuxResourceSample sample = LinuxResourceSampleParser.Parse(output);

            Assert.That(sample.MemoryTotalBytes, Is.EqualTo(8589934592));
        }

        [Test]
        public void CalculatesCpuAndNetworkRatesFromTwoSamples()
        {
            LinuxResourceSample previous = new(100, 40, 8, 4, 100, 50, 1_000, 2_000, TimeSpan.FromSeconds(10));
            LinuxResourceSample current = new(200, 90, 8, 3, 100, 60, 3_000, 5_000, TimeSpan.FromSeconds(15));

            RemoteResourceSnapshot snapshot = RemoteResourceSnapshotCalculator.Calculate(
                current,
                previous,
                TimeSpan.FromSeconds(2));

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.CpuPercent, Is.EqualTo(50));
                Assert.That(snapshot.ReceiveBytesPerSecond, Is.EqualTo(1_000));
                Assert.That(snapshot.TransmitBytesPerSecond, Is.EqualTo(1_500));
                Assert.That(snapshot.DiskPercent, Is.EqualTo(60));
            });
        }

        [Test]
        public void RejectsIncompleteOrUnsafeProbeOutput()
        {
            Assert.That(() => LinuxResourceSampleParser.Parse("cpu_total=10\ncpu_idle=3"),
                Throws.TypeOf<FormatException>());
            Assert.That(() => LinuxResourceSampleParser.Parse("cpu_total=-1\ncpu_idle=0\nmem_total=1\nmem_available=0\ndisk_total=1\ndisk_used=0\nnet_rx=0\nnet_tx=0\nuptime_seconds=0"),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void ProbeSumsTransmitBytesFromEveryNonLoopbackInterface()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LinuxResourceProbe.Command, Does.Contain("iface=\\$1"));
                Assert.That(LinuxResourceProbe.Command, Does.Contain("rx += \\$2; tx += \\$10"));
                Assert.That(LinuxResourceProbe.Command, Does.Contain("iface !~ /^lo$/"));
            });
        }
    }
}
