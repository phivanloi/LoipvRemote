using System;
using System.Drawing;

namespace LoipvRemote.UI.Tabs
{
    internal static class DockTabMetrics
    {
        internal const int TextPadding = 5;

        internal static int BoxedTextHeight(int textHeight)
        {
            if (textHeight <= 0) throw new ArgumentOutOfRangeException(nameof(textHeight));
            return textHeight + (TextPadding * 2);
        }

        internal static int BoxedTextWidth(int textWidth)
        {
            if (textWidth < 0) throw new ArgumentOutOfRangeException(nameof(textWidth));
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
