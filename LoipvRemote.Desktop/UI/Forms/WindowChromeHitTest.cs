using System.Drawing;

namespace LoipvRemote.UI.Forms
{
    internal static class WindowChromeHitTest
    {
        internal const int Client = 1;
        internal const int Caption = 2;
        internal const int Left = 10;
        internal const int Right = 11;
        internal const int Top = 12;
        internal const int TopLeft = 13;
        internal const int TopRight = 14;
        internal const int Bottom = 15;
        internal const int BottomLeft = 16;
        internal const int BottomRight = 17;

        internal static int ResolveResizeHitTest(Size clientSize, Point point, int borderThickness)
        {
            if (clientSize.Width <= borderThickness * 2 || clientSize.Height <= borderThickness * 2)
                return Client;

            bool left = point.X >= 0 && point.X < borderThickness;
            bool right = point.X >= clientSize.Width - borderThickness && point.X < clientSize.Width;
            bool top = point.Y >= 0 && point.Y < borderThickness;
            bool bottom = point.Y >= clientSize.Height - borderThickness && point.Y < clientSize.Height;

            if (top && left) return TopLeft;
            if (top && right) return TopRight;
            if (bottom && left) return BottomLeft;
            if (bottom && right) return BottomRight;
            if (left) return Left;
            if (right) return Right;
            if (top) return Top;
            return bottom ? Bottom : Client;
        }

        internal static bool IsCaptionDoubleClick(int nonClientHitTest)
        {
            return nonClientHitTest == Caption;
        }
    }
}
