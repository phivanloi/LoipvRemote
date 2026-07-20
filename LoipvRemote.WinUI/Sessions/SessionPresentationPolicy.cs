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

    public static string FormatConnectionFailure(string reason) =>
        $"Connection failed: {reason}";
}
