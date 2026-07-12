using System;

namespace LoipvRemote.UI.DesignSystem
{
    internal static class InputControlMetrics
    {
        internal static int InputHeight(int textHeight)
        {
            if (textHeight <= 0) throw new ArgumentOutOfRangeException(nameof(textHeight));
            return Math.Max(26, textHeight + 10);
        }

        internal static int CheckBoxGlyphSize(int textHeight)
        {
            if (textHeight <= 0) throw new ArgumentOutOfRangeException(nameof(textHeight));
            return Math.Clamp(textHeight, 14, 20);
        }

        internal static int ComboBoxItemHeight(int textHeight)
        {
            if (textHeight <= 0) throw new ArgumentOutOfRangeException(nameof(textHeight));
            return Math.Max(22, textHeight + 8);
        }
    }
}
