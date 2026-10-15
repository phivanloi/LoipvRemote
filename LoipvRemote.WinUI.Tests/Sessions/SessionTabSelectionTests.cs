using LoipvRemote.WinUI.Sessions;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Sessions;

public sealed class SessionTabSelectionTests
{
    [TestCase(0, 1, 1)]
    [TestCase(2, 1, 0)]
    [TestCase(0, -1, 2)]
    [TestCase(1, -1, 0)]
    [TestCase(0, 4, 1)]
    [TestCase(2, -5, 0)]
    public void SelectRelativeWrapsAcrossSessionTabs(int currentIndex, int direction, int expectedIndex)
    {
        object[] tabs = [new(), new(), new()];

        object? selected = SessionTabSelection.SelectRelative(tabs, tabs[currentIndex], direction);

        Assert.That(selected, Is.SameAs(tabs[expectedIndex]));
    }

    [Test]
    public void SelectRelativeSkipsReattachmentWhenTheCombinedOffsetReturnsToCurrentTab()
    {
        object[] tabs = [new(), new(), new()];

        object? selected = SessionTabSelection.SelectRelative(tabs, tabs[1], 30);

        Assert.That(selected, Is.Null);
    }

    [Test]
    public void SelectRelativeReturnsNullWithoutEnoughTabs()
    {
        var onlyTab = new object();

        Assert.That(SessionTabSelection.SelectRelative([onlyTab], onlyTab, 1), Is.Null);
    }

    [Test]
    public void SelectAfterClosePrefersTheNextTab()
    {
        var first = new object();
        var closing = new object();
        var next = new object();

        object? selected = SessionTabSelection.SelectAfterClose([first, closing, next], closing);

        Assert.That(selected, Is.SameAs(next));
    }

    [Test]
    public void SelectAfterCloseUsesPreviousTabWhenClosingTheLastTab()
    {
        var previous = new object();
        var closing = new object();

        object? selected = SessionTabSelection.SelectAfterClose([previous, closing], closing);

        Assert.That(selected, Is.SameAs(previous));
    }
}
