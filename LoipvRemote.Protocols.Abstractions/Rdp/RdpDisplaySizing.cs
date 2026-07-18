namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Creates predictable, DPI-aware display settings for embedded RDP tabs.</summary>
public static class RdpDisplaySizing
{
    public const int MinimumWidth = 1024;
    public const int MinimumHeight = 768;
    public const int MaximumWidth = 3840;
    public const int MaximumHeight = 2160;
    private const int ResolutionStep = 8;
    private static readonly uint[] SupportedDesktopScales = [100, 125, 150, 175, 200, 250, 300, 400, 500];

    /// <summary>
    /// Produces a stable RDP desktop size from the native tab content bounds.
    /// Bounds are in physical pixels; small tabs use SmartSizing rather than
    /// negotiating a desktop too small for normal Windows UI.
    /// </summary>
    public static RdpDisplayConfiguration CreateAuto(int physicalWidth, int physicalHeight, double rasterizationScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(physicalWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(physicalHeight);
        if (double.IsNaN(rasterizationScale) || double.IsInfinity(rasterizationScale) || rasterizationScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(rasterizationScale));

        int width = RoundToStep(Math.Clamp(physicalWidth, MinimumWidth, MaximumWidth));
        int height = RoundToStep(Math.Clamp(physicalHeight, MinimumHeight, MaximumHeight));
        uint desktopScale = ToSupportedDesktopScale(rasterizationScale);
        return new RdpDisplayConfiguration(width, height, false, true, desktopScale, 100);
    }

    private static int RoundToStep(int value) =>
        (int)Math.Round(value / (double)ResolutionStep, MidpointRounding.AwayFromZero) * ResolutionStep;

    private static uint ToSupportedDesktopScale(double rasterizationScale)
    {
        uint requested = (uint)Math.Round(rasterizationScale * 100, MidpointRounding.AwayFromZero);
        return SupportedDesktopScales.MinBy(scale => Math.Abs((long)scale - requested));
    }
}
