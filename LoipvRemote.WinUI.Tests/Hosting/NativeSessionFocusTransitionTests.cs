using LoipvRemote.WinUI.Hosting;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class NativeSessionFocusTransitionTests
{
    [Test]
    public void ActivationFocusesTheNativeSessionBeforeQueuingASettledLayoutRetry()
    {
        var calls = new List<string>();

        NativeSessionFocusTransition.Restore(
            focusImmediately: () => calls.Add("focus"),
            queueSettledLayoutRetry: () => calls.Add("retry"));

        Assert.That(calls, Is.EqualTo(["focus", "retry"]));
    }
}
