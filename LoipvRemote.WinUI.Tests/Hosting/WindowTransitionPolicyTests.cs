using LoipvRemote.WinUI.Hosting;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class WindowTransitionPolicyTests
{
    [Test]
    public void MinimizeSuspendsNativeSessionRefresh()
    {
        bool shouldRefresh = WindowTransitionPolicy.ShouldRefreshNativeSession(
            isMinimized: true,
            didPositionChange: true,
            didSizeChange: false,
            didPresenterChange: true);

        Assert.That(shouldRefresh, Is.False);
    }

    [Test]
    public void RestoredWindowRefreshesNativeSessionAfterAWindowChange()
    {
        bool shouldRefresh = WindowTransitionPolicy.ShouldRefreshNativeSession(
            isMinimized: false,
            didPositionChange: false,
            didSizeChange: true,
            didPresenterChange: true);

        Assert.That(shouldRefresh, Is.True);
    }

    [Test]
    public void MinimizedOwnerDoesNotProduceNativeBounds()
    {
        bool shouldUpdate = WindowTransitionPolicy.ShouldUpdateNativeBounds(
            isMinimized: true,
            actualWidth: 1773,
            actualHeight: 1034);

        Assert.That(shouldUpdate, Is.False);
    }
}
