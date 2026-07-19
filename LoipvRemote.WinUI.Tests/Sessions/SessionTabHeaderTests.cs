using LoipvRemote.WinUI.Sessions;
using Microsoft.UI;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Sessions;

public sealed class SessionTabHeaderTests
{
    [TestCase(false, false)]
    [TestCase(true, true)]
    public void UsesActiveForegroundOnlyForTheSelectedTab(bool isActive, bool expected)
    {
        Assert.That(SessionTabHeader.UsesActiveForeground(isActive), Is.EqualTo(expected));
    }

    [Test]
    public void InactiveTabIconIsOpaqueBlackInsteadOfTransparent()
    {
        Assert.That(SessionTabHeader.GetIconColor(isActive: false), Is.EqualTo(Colors.Black));
    }

    [Test]
    public void ActiveTabIconUsesTheConnectedGreen()
    {
        Assert.That(SessionTabHeader.GetIconColor(isActive: true), Is.EqualTo(Colors.ForestGreen));
    }
}
