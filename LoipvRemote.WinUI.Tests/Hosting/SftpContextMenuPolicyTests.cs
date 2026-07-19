using LoipvRemote.WinUI;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class SftpContextMenuPolicyTests
{
    [TestCase(SftpPaneSide.Local, false, SftpEntryAction.Upload, SftpEntryAction.Rename, SftpEntryAction.Delete)]
    [TestCase(SftpPaneSide.Remote, false, SftpEntryAction.Download, SftpEntryAction.Rename, SftpEntryAction.Delete)]
    [TestCase(SftpPaneSide.Local, true, SftpEntryAction.Open, SftpEntryAction.Rename, SftpEntryAction.Delete)]
    [TestCase(SftpPaneSide.Remote, true, SftpEntryAction.Open, SftpEntryAction.Rename, SftpEntryAction.Delete)]
    public void ContextMenuActionsMatchThePaneAndEntryType(
        SftpPaneSide side,
        bool isDirectory,
        params SftpEntryAction[] expected)
    {
        Assert.That(SftpContextMenuPolicy.For(side, isDirectory), Is.EqualTo(expected));
    }

    [TestCase(2048, 1104, 1555, 878)]
    [TestCase(1530, 800, 1450, 720)]
    [TestCase(1280, 720, 1200, 640)]
    public void DialogSizeUsesTheLargerTargetWithoutExceedingTheAvailableArea(
        double availableWidth,
        double availableHeight,
        double expectedWidth,
        double expectedHeight)
    {
        SftpDialogSize size = SftpDialogSizing.Fit(availableWidth, availableHeight);

        Assert.Multiple(() =>
        {
            Assert.That(size.Width, Is.EqualTo(expectedWidth));
            Assert.That(size.Height, Is.EqualTo(expectedHeight));
            Assert.That(size.Width, Is.LessThan(availableWidth));
            Assert.That(size.Height, Is.LessThan(availableHeight));
        });
    }

    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(false, false, false)]
    public void WindowStaysTopmostOnlyWhileLoipvRemoteOwnsTheForeground(
        bool foregroundUsesAppProcess,
        bool foregroundIsInsideOwner,
        bool expected)
    {
        Assert.That(
            SftpWindowActivationPolicy.ShouldStayTopmost(
                foregroundUsesAppProcess,
                foregroundIsInsideOwner),
            Is.EqualTo(expected));
    }
}
