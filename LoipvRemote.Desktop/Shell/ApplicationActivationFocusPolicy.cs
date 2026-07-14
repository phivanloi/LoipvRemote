namespace LoipvRemote.Desktop.Shell;

/// <summary>Determines whether the active embedded connection should regain focus after app activation.</summary>
public static class ApplicationActivationFocusPolicy
{
    private const int WmActivateApplication = 0x001C;

    public static bool ShouldRestoreActiveConnectionFocus(int message, IntPtr wParam) =>
        message == WmActivateApplication && wParam != IntPtr.Zero;
}
