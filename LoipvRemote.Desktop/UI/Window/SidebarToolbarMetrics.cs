namespace LoipvRemote.UI.Window;

internal sealed record SidebarToolbarLayout(
    int IconSize,
    int Height,
    int ItemWidth,
    int HorizontalPadding);

internal static class SidebarToolbarMetrics
{
    public static SidebarToolbarLayout ForDpi(
        int dpi,
        int iconSize,
        int interactiveHeight,
        int iconHitTarget)
    {
        float scale = Math.Max(96, dpi) / 96f;
        int Scale(int logicalPixels) => (int)Math.Round(logicalPixels * scale);
        int scaledIconSize = Scale(iconSize);
        int iconSpacing = Scale(16);

        return new SidebarToolbarLayout(
            scaledIconSize,
            Math.Max(Scale(interactiveHeight), scaledIconSize + iconSpacing),
            Math.Max(Scale(iconHitTarget), scaledIconSize + iconSpacing),
            Scale(8));
    }
}
