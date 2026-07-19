using LoipvRemote.WinUI.Sessions;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Sessions;

public sealed class SessionTabPointerPolicyTests
{
    [TestCase(10, 10, 0, 0, 20, 20, true)]
    [TestCase(20, 10, 0, 0, 20, 20, false)]
    [TestCase(-1, 10, 0, 0, 20, 20, false)]
    [TestCase(10, 10, 0, 0, 0, 20, false)]
    public void ContainsPointUsesHalfOpenTabBounds(
        double pointX,
        double pointY,
        double left,
        double top,
        double width,
        double height,
        bool expected)
    {
        Assert.That(
            SessionTabPointerPolicy.ContainsPoint(pointX, pointY, left, top, width, height),
            Is.EqualTo(expected));
    }

    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, false)]
    public void MiddleClickClosesOnlyClosableSessionTabs(
        bool middleButtonPressed,
        bool isClosable,
        bool expected)
    {
        Assert.That(
            SessionTabPointerPolicy.ShouldClose(middleButtonPressed, isClosable),
            Is.EqualTo(expected));
    }
}
