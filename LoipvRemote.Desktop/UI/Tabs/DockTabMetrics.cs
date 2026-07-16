using System;
using System.Drawing;

namespace LoipvRemote.UI.Tabs
{
    internal static class DockTabMetrics
    {
        internal const int TextPadding = 5;

        internal static int BoxedTextHeight(int textHeight)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textHeight);
            return textHeight + (TextPadding * 2);
        }

        internal static int BoxedTextWidth(int textWidth)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(textWidth);
            return textWidth + (TextPadding * 2);
        }

        internal static Rectangle TextBounds(Rectangle tabBounds)
        {
            int width = Math.Max(0, tabBounds.Width - (TextPadding * 2));
            int height = Math.Max(0, tabBounds.Height - (TextPadding * 2));
            return new Rectangle(tabBounds.X + TextPadding, tabBounds.Y + TextPadding, width, height);
        }
    }
}
