namespace LoipvRemote.Desktop.Sessions;

/// <summary>
/// Bounds focus retries while an embedded protocol process finishes creating
/// or recreating its native window after a tab switch.
/// </summary>
public static class ProtocolFocusRetryPolicy
{
    // PuTTY creates its hosted child window asynchronously after the process
    // reports started. Give the native window enough time to appear while
    // keeping retries bounded and entirely on the WinForms UI timer.
    public const int MaxAttempts = 40;
    public const int IntervalMilliseconds = 100;

    public static bool ShouldAttempt(int attempt, bool active)
    {
        return active && attempt >= 0 && attempt < MaxAttempts;
    }
}
