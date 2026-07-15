using System;
using System.Drawing;

namespace LoipvRemote.UI.Tabs
{
    internal static class DocumentTabMetrics
    {
        internal const int ContentPadding = 5;
        internal const int CloseButtonSize = 22;
        internal const int MaximumWidth = 240;

        internal static int MinimumHeight(int textHeight, int iconHeight)
        {
            if (textHeight <= 0) throw new ArgumentOutOfRangeException(nameof(textHeight));
            if (iconHeight < 0) throw new ArgumentOutOfRangeException(nameof(iconHeight));

            return Math.Max(Math.Max(textHeight, iconHeight), CloseButtonSize) + (ContentPadding * 2);
        }

        internal static Rectangle ContentBounds(Rectangle tabBounds)
        {
            return new Rectangle(tabBounds.X + ContentPadding,
                                 tabBounds.Y + ContentPadding,
                                 Math.Max(0, tabBounds.Width - (ContentPadding * 2)),
                                 Math.Max(0, tabBounds.Height - (ContentPadding * 2)));
        }

        internal static Rectangle CloseButtonBounds(Rectangle tabBounds)
        {
            Rectangle contentBounds = ContentBounds(tabBounds);
            int size = Math.Min(CloseButtonSize, Math.Min(contentBounds.Width, contentBounds.Height));
            return new Rectangle(contentBounds.Right - size,
                                 contentBounds.Y + (contentBounds.Height - size) / 2,
                                 size,
                                 size);
        }
    }
}
