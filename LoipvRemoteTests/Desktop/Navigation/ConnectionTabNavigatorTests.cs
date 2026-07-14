using LoipvRemote.UseCases.Navigation;
using NUnit.Framework;

namespace LoipvRemoteTests.Desktop.Navigation;

public class ConnectionTabNavigatorTests
{
    [TestCase(0, 1)]
    [TestCase(1, 2)]
    [TestCase(2, 0)]
    public void NextNavigationWrapsAround(int activeIndex, int expectedIndex)
    {
        bool found = ConnectionTabNavigator.TryGetRelativeIndex(3, activeIndex, 1, out int targetIndex);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(targetIndex, Is.EqualTo(expectedIndex));
        });
    }

    [TestCase(0, 2)]
    [TestCase(1, 0)]
    [TestCase(2, 1)]
    public void PreviousNavigationWrapsAround(int activeIndex, int expectedIndex)
    {
        bool found = ConnectionTabNavigator.TryGetRelativeIndex(3, activeIndex, -1, out int targetIndex);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(targetIndex, Is.EqualTo(expectedIndex));
        });
    }

    [TestCase(0, -1)]
    [TestCase(1, -1)]
    [TestCase(3, 3)]
    public void RelativeNavigationRejectsInvalidOrSingleTabCollections(int tabCount, int activeIndex)
    {
        Assert.That(ConnectionTabNavigator.TryGetRelativeIndex(tabCount, activeIndex, 1, out _), Is.False);
    }

    [TestCase(1, 0, true)]
    [TestCase(3, 2, true)]
    [TestCase(3, -1, false)]
    [TestCase(3, 3, false)]
    public void ValidatesExplicitTabIndex(int tabCount, int index, bool expected)
    {
        Assert.That(ConnectionTabNavigator.IsValidIndex(tabCount, index), Is.EqualTo(expected));
    }
}
