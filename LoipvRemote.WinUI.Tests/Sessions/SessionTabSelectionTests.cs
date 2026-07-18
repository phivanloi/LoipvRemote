using LoipvRemote.WinUI.Sessions;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Sessions;

public sealed class SessionTabSelectionTests
{
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
