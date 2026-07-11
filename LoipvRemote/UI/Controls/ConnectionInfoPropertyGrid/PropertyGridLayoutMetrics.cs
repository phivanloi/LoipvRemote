using System;

namespace LoipvRemote.UI.Controls.ConnectionInfoPropertyGrid
{
    internal static class PropertyGridLayoutMetrics
    {
        private const int MinimumRowHeight = 24;
        private const int VerticalTextPadding = 8;

        internal static int RowHeightForFontHeight(int fontHeight)
        {
            if (fontHeight <= 0) throw new ArgumentOutOfRangeException(nameof(fontHeight));

            return Math.Max(MinimumRowHeight, fontHeight + VerticalTextPadding);
        }
    }
}
