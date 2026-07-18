using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests;

public sealed class TitleBarLayoutMetricsTests
{
    [Test]
    public void UsesTheSidebarWidthAsTheExactTabStart()
    {
        Assert.That(TitleBarLayoutMetrics.SidebarWidth, Is.EqualTo(275));
    }

    [Test]
    public void UsesAStandardInteractiveTitleBar()
    {
        Assert.That(TitleBarLayoutMetrics.Height, Is.EqualTo(32));
    }

    [TestCase(138, 1.25, 110.4)]
    [TestCase(180, 1.5, 120)]
    public void ConvertsCaptionInsetsFromPhysicalToLogicalPixels(int physicalPixels, double scale, double expected)
    {
        Assert.That(TitleBarLayoutMetrics.ToLogicalPixels(physicalPixels, scale), Is.EqualTo(expected).Within(0.001));
    }
}
