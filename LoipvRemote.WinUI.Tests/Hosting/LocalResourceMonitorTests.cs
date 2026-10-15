using LoipvRemote.Infrastructure.Windows.Monitoring;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class LocalResourceMonitorTests
{
    [Test]
    public void CpuPercentUsesIdleKernelAndUserDeltas()
    {
        var previous = new LocalResourceMonitor.CpuTimes(100, 300, 200);
        var current = new LocalResourceMonitor.CpuTimes(150, 400, 300);

        double? percent = LocalResourceMonitor.CalculateCpuPercent(previous, current);

        Assert.That(percent, Is.EqualTo(75d));
    }

    [Test]
    public void NetworkRateUsesElapsedStopwatchTicks()
    {
        var previous = new LocalResourceMonitor.NetworkSample(100, 1_000, 2_000);
        var current = new LocalResourceMonitor.NetworkSample(
            100 + System.Diagnostics.Stopwatch.Frequency * 2L,
            3_000,
            8_000);

        long? received = LocalResourceMonitor.CalculateNetworkRate(previous, current, sample => sample.ReceivedBytes);
        long? sent = LocalResourceMonitor.CalculateNetworkRate(previous, current, sample => sample.SentBytes);

        Assert.Multiple(() =>
        {
            Assert.That(received, Is.EqualTo(1_000));
            Assert.That(sent, Is.EqualTo(3_000));
        });
    }
}
