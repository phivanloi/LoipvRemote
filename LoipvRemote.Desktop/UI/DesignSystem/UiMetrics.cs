using System;

namespace LoipvRemote.UI.DesignSystem
{
    public enum UiTypographyRole
    {
        Small,
        Body,
        Title,
        LargeTitle,
        Monospace
    }

    public sealed class UiMetrics
    {
        public UiMetrics(UiPreferences preferences)
        {
            Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        }

        public UiPreferences Preferences { get; }
        public float FontScale => Preferences.FontScalePercent / 100f;
        public int IconSize => Preferences.IconSize;
        public float SmallFontPoints => 9f * FontScale;
        public float BodyFontPoints => 10.5f * FontScale;
        public float TitleFontPoints => 12f * FontScale;
        public float LargeTitleFontPoints => 14f * FontScale;
        public float MonospaceFontPoints => 10f * FontScale;

        public int InteractiveHeight => Preferences.Density switch
        {
            UiDensity.Compact => 32,
            UiDensity.Comfortable => 40,
            _ => 36
        };

        public int RowHeight => Preferences.Density switch
        {
            UiDensity.Compact => 28,
            UiDensity.Comfortable => 36,
            _ => 32
        };

        public int IconHitTarget => Math.Max(32, InteractiveHeight);

        public float FontPoints(UiTypographyRole role) => role switch
        {
            UiTypographyRole.Small => SmallFontPoints,
            UiTypographyRole.Title => TitleFontPoints,
            UiTypographyRole.LargeTitle => LargeTitleFontPoints,
            UiTypographyRole.Monospace => MonospaceFontPoints,
            _ => BodyFontPoints
        };

        public int ScaleForDpi(int logicalPixels, float dpiScale)
        {
            if (dpiScale <= 0) throw new ArgumentOutOfRangeException(nameof(dpiScale));
            return (int)Math.Round(logicalPixels * dpiScale);
        }
    }
}
