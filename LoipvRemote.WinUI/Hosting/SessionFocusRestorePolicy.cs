namespace LoipvRemote.WinUI.Hosting;

/// <summary>
/// Prevents an embedded native session from reclaiming focus while a pointer
/// click is still completing on a WinUI shell or native caption control.
/// </summary>
internal static class SessionFocusRestorePolicy
{
    private const long ShellClickSuppressionMilliseconds = 750;

    public static long CreateShellClickSuppressionDeadline(long currentTimestamp) =>
        currentTimestamp + ShellClickSuppressionMilliseconds;

    public static bool ShouldSuppressForShellClick(
        long suppressionDeadline,
        long currentTimestamp) =>
        suppressionDeadline > currentTimestamp;
}
