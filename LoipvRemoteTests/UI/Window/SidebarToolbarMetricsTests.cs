using LoipvRemote.UI.Window;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Window;

public sealed class SidebarToolbarMetricsTests
{
    [Test]
    public void ToolbarHitTargetsAndIconsScaleAtOneHundredTwentyFivePercentDpi()
    {
        SidebarToolbarLayout layout = SidebarToolbarMetrics.ForDpi(120, iconSize: 20,
            interactiveHeight: 36, iconHitTarget: 36);

        Assert.Multiple(() =>
        {
            Assert.That(layout.IconSize, Is.EqualTo(25));
            Assert.That(layout.Height, Is.EqualTo(45));
            Assert.That(layout.ItemWidth, Is.EqualTo(45));
            Assert.That(layout.HorizontalPadding, Is.EqualTo(10));
        });
    }
}
