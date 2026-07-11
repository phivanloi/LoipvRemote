using System;
using System.Drawing;

namespace LoipvRemote.UI.Forms
{
    internal static class UnifiedWindowLayout
    {
        internal static Rectangle ContentBounds(Size clientSize, int headerHeight, bool headerVisible)
        {
            int occupiedHeight = headerVisible ? Math.Clamp(headerHeight, 0, clientSize.Height) : 0;
            return new Rectangle(0, occupiedHeight, clientSize.Width, clientSize.Height - occupiedHeight);
        }
    }
}
