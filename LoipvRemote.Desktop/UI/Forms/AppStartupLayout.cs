using System;
using System.Windows.Forms;

namespace LoipvRemote.UI.Forms
{
    internal static class AppStartupLayout
    {
        // This is a shell pixel width, not a logical font measurement. The
        // docking library is applied after WinForms has already scaled the
        // shell, so scaling this value again makes the left panel too wide.
        internal const int DefaultSidebarWidth = 340;

        internal static double SidebarPortionForWidth(int dockPanelWidth, int deviceDpi = 96)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dockPanelWidth);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deviceDpi);

            // DockPanelSuite's DockLeftPortion is a ratio, not a pixel width.
            // WinForms reports ClientSize in logical units at a non-100% DPI,
            // while the requested shell width is physical pixels. Convert the
            // physical width to the logical coordinate system before deriving
            // the ratio, otherwise a 125% display produces a 272px sidebar.
            double logicalSidebarWidth = DefaultSidebarWidth * deviceDpi / 96d;
            return Math.Clamp(logicalSidebarWidth / dockPanelWidth, 0.1, 0.5);
        }

        internal static FormWindowState ResolveWindowState(bool startMinimized, bool startFullScreen)
        {
            if (startMinimized) return FormWindowState.Minimized;
            return startFullScreen ? FormWindowState.Normal : FormWindowState.Maximized;
        }
    }
}
