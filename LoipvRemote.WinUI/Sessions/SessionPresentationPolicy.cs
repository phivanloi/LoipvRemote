namespace LoipvRemote.WinUI.Sessions;

internal enum SessionPresentationTrigger
{
    TabSelection,
    ConnectionCompleted
}

internal static class SessionPresentationPolicy
{
    public static bool ShouldActivateNativeSession(SessionPresentationTrigger trigger) =>
        trigger == SessionPresentationTrigger.TabSelection;

    public static bool ShouldDeactivateNativeSurface(RemoteSessionTabState state) =>
        state != RemoteSessionTabState.Connected;

    public static bool ShouldRestoreSelectedSession<T>(T completedSession, T selectedSession)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(completedSession);
        ArgumentNullException.ThrowIfNull(selectedSession);
        return !ReferenceEquals(completedSession, selectedSession);
    }

    public static string FormatConnectionFailure(string reason) =>
        $"Connection failed: {reason}";
}
