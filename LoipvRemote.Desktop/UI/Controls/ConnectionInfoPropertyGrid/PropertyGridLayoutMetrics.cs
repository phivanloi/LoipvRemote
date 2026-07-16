using System;

namespace LoipvRemote.UI.Controls.ConnectionInfoPropertyGrid
{
    internal static class PropertyGridLayoutMetrics
    {
        private const int MinimumRowHeight = 24;
        private const int VerticalTextPadding = 8;

        internal static int RowHeightForFontHeight(int fontHeight)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontHeight);

            return Math.Max(MinimumRowHeight, fontHeight + VerticalTextPadding);
        }
    }
}
