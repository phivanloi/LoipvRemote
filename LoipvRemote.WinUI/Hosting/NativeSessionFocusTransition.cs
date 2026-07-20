namespace LoipvRemote.WinUI.Hosting;

/// <summary>
/// Restores native keyboard focus immediately after app activation, then retries
/// once the WinUI layout has settled so the first user keystroke is never lost.
/// </summary>
internal static class NativeSessionFocusTransition
{
    public static void Restore(Action focusImmediately, Action queueSettledLayoutRetry)
    {
        ArgumentNullException.ThrowIfNull(focusImmediately);
        ArgumentNullException.ThrowIfNull(queueSettledLayoutRetry);

        focusImmediately();
        queueSettledLayoutRetry();
    }

    public static async Task WaitUntilUnblockedAsync(
        Func<bool> isFocusBlocked,
        TimeSpan retryInterval,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(isFocusBlocked);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryInterval, TimeSpan.Zero);

        while (isFocusBlocked())
            await Task.Delay(retryInterval, cancellationToken).ConfigureAwait(false);
    }
}
