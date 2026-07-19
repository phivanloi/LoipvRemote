namespace LoipvRemote.WinUI.Hosting;

/// <summary>Filters transient minimized-window geometry before it reaches native session hosts.</summary>
internal static class WindowTransitionPolicy
{
    public static bool ShouldRefreshNativeSession(
        bool isMinimized,
        bool didPositionChange,
        bool didSizeChange,
        bool didPresenterChange) =>
        !isMinimized && (didPositionChange || didSizeChange || didPresenterChange);

    public static bool ShouldUpdateNativeBounds(bool isMinimized, double actualWidth, double actualHeight) =>
        !isMinimized && actualWidth > 0 && actualHeight > 0;
}
