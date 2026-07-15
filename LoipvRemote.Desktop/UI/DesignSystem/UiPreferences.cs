using System;

namespace LoipvRemote.UI.DesignSystem
{
    public enum UiDensity
    {
        Compact,
        Standard,
        Comfortable
    }

    public sealed record UiPreferences
    {
        public const int DefaultFontScalePercent = 100;
        public const int DefaultIconSize = 20;

        public UiPreferences(string? fontFamily, int fontScalePercent, int iconSize, UiDensity density)
        {
            FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "System" : fontFamily.Trim();
            FontScalePercent = Math.Clamp(fontScalePercent, 90, 150);
            IconSize = Math.Clamp(iconSize, 16, 28);
            Density = density;
        }

        public string FontFamily { get; }
        public int FontScalePercent { get; }
        public int IconSize { get; }
        public UiDensity Density { get; }

        public static UiPreferences FromSettings()
        {
            Properties.OptionsAppearancePage settings = Properties.OptionsAppearancePage.Default;
            UiDensity density = Enum.TryParse(settings.UiDensity, true, out UiDensity parsed)
                ? parsed
                : UiDensity.Standard;
            return new UiPreferences(settings.UiFontFamily, settings.UiFontScalePercent, settings.UiIconSize, density);
        }
    }
}
