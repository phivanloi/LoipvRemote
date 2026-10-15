using LoipvRemote.WinUI.Hosting;
using NUnit.Framework;
using System.Runtime.InteropServices;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class NativeUiExceptionGuardTests
{
    [Test]
    public void TryRunCapturesRecoverableComFailure()
    {
        Exception? reported = null;

        bool completed = NativeUiExceptionGuard.TryRun(
            () => Marshal.ThrowExceptionForHR(unchecked((int)0x80004005)),
            exception => reported = exception);

        Assert.Multiple(() =>
        {
            Assert.That(completed, Is.False);
            Assert.That(reported, Is.TypeOf<COMException>());
            Assert.That(reported!.HResult, Is.EqualTo(unchecked((int)0x80004005)));
        });
    }

    [Test]
    public void TryRunDoesNotMaskSuccessfulLayoutWork()
    {
        bool invoked = false;

        bool completed = NativeUiExceptionGuard.TryRun(
            () => invoked = true,
            _ => Assert.Fail("A successful layout operation must not be reported as a failure."));

        Assert.Multiple(() =>
        {
            Assert.That(completed, Is.True);
            Assert.That(invoked, Is.True);
        });
    }
}
