using LoipvRemote.WinUI.Hosting;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class SessionFocusRestorePolicyTests
{
    [Test]
    public void ShellClickSuppressesEmbeddedFocusLongEnoughForCaptionButtonClickToFinish()
    {
        const long clickTimestamp = 10_000;

        long suppressionDeadline = SessionFocusRestorePolicy.CreateShellClickSuppressionDeadline(
            clickTimestamp);

        Assert.Multiple(() =>
        {
            Assert.That(
                SessionFocusRestorePolicy.ShouldSuppressForShellClick(
                    suppressionDeadline,
                    clickTimestamp),
                Is.True);
            Assert.That(
                SessionFocusRestorePolicy.ShouldSuppressForShellClick(
                    suppressionDeadline,
                    suppressionDeadline - 1),
                Is.True);
            Assert.That(
                SessionFocusRestorePolicy.ShouldSuppressForShellClick(
                    suppressionDeadline,
                    suppressionDeadline),
                Is.False);
        });
    }

    [TestCase(0, 10_000, false)]
    [TestCase(9_999, 10_000, false)]
    [TestCase(10_001, 10_000, true)]
    public void SuppressionRequiresAFutureDeadline(
        long suppressionDeadline,
        long currentTimestamp,
        bool expected)
    {
        Assert.That(
            SessionFocusRestorePolicy.ShouldSuppressForShellClick(
                suppressionDeadline,
                currentTimestamp),
            Is.EqualTo(expected));
    }
}
