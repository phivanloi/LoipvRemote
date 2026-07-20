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

    [Test]
    public async Task DeferredFocusWaitsUntilTheNativeSecurityDialogCloses()
    {
        bool focusBlocked = true;

        Task wait = NativeSessionFocusTransition.WaitUntilUnblockedAsync(
            () => focusBlocked,
            TimeSpan.FromMilliseconds(10),
            CancellationToken.None);

        await Task.Delay(30);
        Assert.That(wait.IsCompleted, Is.False);

        focusBlocked = false;
        await wait.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(wait.IsCompletedSuccessfully, Is.True);
    }
}
