using System.Drawing;

namespace LoipvRemote.UI.Forms
{
    internal static class WindowMaximizeBounds
    {
        internal static Rectangle Resolve(Rectangle monitorBounds, Rectangle workingArea)
        {
            return workingArea.IsEmpty ? monitorBounds : workingArea;
        }
    }
}
