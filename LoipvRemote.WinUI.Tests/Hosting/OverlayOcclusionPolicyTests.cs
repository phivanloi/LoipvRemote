using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.WinUI.Hosting;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class OverlayOcclusionPolicyTests
{
    [Test]
    public void PopupIntersectionBecomesAHostLocalHole()
    {
        var host = new EmbeddedWindowBounds(300, 100, 1000, 700);
        var popup = new EmbeddedWindowBounds(250, 50, 200, 200);

        EmbeddedWindowBounds? result = OverlayOcclusionPolicy.ToHostLocalHole(
            host,
            popup,
            padding: 0);

        Assert.That(result, Is.EqualTo(new EmbeddedWindowBounds(0, 0, 150, 150)));
    }

    [Test]
    public void PopupPaddingIsClippedToTheRemoteHost()
    {
        var host = new EmbeddedWindowBounds(300, 100, 1000, 700);
        var popup = new EmbeddedWindowBounds(350, 150, 100, 100);

        EmbeddedWindowBounds? result = OverlayOcclusionPolicy.ToHostLocalHole(
            host,
            popup,
            padding: 4);

        Assert.That(result, Is.EqualTo(new EmbeddedWindowBounds(46, 46, 108, 108)));
    }

    [TestCase(0, 0, 100, 100)]
    [TestCase(1301, 801, 50, 50)]
    public void PopupOutsideTheRemoteHostDoesNotChangeItsRegion(
        int x,
        int y,
        int width,
        int height)
    {
        var host = new EmbeddedWindowBounds(300, 100, 1000, 700);
        var popup = new EmbeddedWindowBounds(x, y, width, height);

        Assert.That(
            OverlayOcclusionPolicy.ToHostLocalHole(host, popup, padding: 0),
            Is.Null);
    }
}
