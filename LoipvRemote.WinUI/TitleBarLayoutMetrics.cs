namespace LoipvRemote.WinUI;

/// <summary>
/// Shared dimensions for the integrated window title bar. Values are in XAML
/// logical pixels so the sidebar and the first session tab stay aligned at any
/// display scale.
/// </summary>
public static class TitleBarLayoutMetrics
{
    public const double SidebarWidth = 275;
    public const double Height = 32;

    public static double ToLogicalPixels(int physicalPixels, double rasterizationScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rasterizationScale);

        return physicalPixels / rasterizationScale;
    }
}
