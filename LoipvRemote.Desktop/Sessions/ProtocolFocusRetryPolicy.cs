namespace LoipvRemote.Desktop.Sessions;

/// <summary>
/// Bounds focus retries while an embedded protocol process finishes creating
/// or recreating its native window after a tab switch.
/// </summary>
public static class ProtocolFocusRetryPolicy
{
    public const int MaxAttempts = 4;
    public const int IntervalMilliseconds = 75;

    public static bool ShouldAttempt(int attempt, bool active)
    {
        return active && attempt >= 0 && attempt < MaxAttempts;
    }
}
