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

    [Test]
    public async Task DeferredFocusRetriesUntilTheSelectedNativeSessionOwnsFocus()
    {
        int attempts = 0;

        bool focused = await NativeSessionFocusTransition.RestoreUntilSuccessfulAsync(
            _ => ValueTask.FromResult(++attempts == 3),
            canContinue: () => true,
            retryDelays: [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(focused, Is.True);
            Assert.That(attempts, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task DeferredFocusStopsWhenItsSessionIsNoLongerSelected()
    {
        int attempts = 0;
        bool selected = true;

        bool focused = await NativeSessionFocusTransition.RestoreUntilSuccessfulAsync(
            _ =>
            {
                attempts++;
                selected = false;
                return ValueTask.FromResult(false);
            },
            canContinue: () => selected,
            retryDelays: [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(focused, Is.False);
            Assert.That(attempts, Is.EqualTo(1));
        });
    }
}
