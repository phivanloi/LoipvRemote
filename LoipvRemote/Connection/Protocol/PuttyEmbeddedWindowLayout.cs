using System;
using System.Drawing;
using LoipvRemote.App;

namespace LoipvRemote.Connection.Protocol
{
    internal static class PuttyEmbeddedWindowLayout
    {
        internal static int CreateBorderlessChildStyle(int style)
        {
            const int nonClientChrome = NativeMethods.WS_CAPTION |
                                       NativeMethods.WS_THICKFRAME |
                                       NativeMethods.WS_SYSMENU |
                                       NativeMethods.WS_MINIMIZEBOX |
                                       NativeMethods.WS_MAXIMIZEBOX |
                                       NativeMethods.WS_POPUP;

            return (style & ~nonClientChrome) | NativeMethods.WS_CHILD;
        }

        internal static Rectangle ContentBounds(Rectangle clientRectangle)
        {
            return clientRectangle;
        }

        internal static Rectangle ContentBounds(Rectangle clientRectangle, int titleStripHeight)
        {
            if (titleStripHeight < 0) throw new ArgumentOutOfRangeException(nameof(titleStripHeight));

            // PuTTY keeps its former title area in the client surface after
            // its non-client chrome is removed. Move the child upward and
            // extend it by the same amount so that area is clipped by the host.
            return new Rectangle(clientRectangle.X,
                                 clientRectangle.Y - titleStripHeight,
                                 clientRectangle.Width,
                                 clientRectangle.Height + titleStripHeight);
        }
    }
}
