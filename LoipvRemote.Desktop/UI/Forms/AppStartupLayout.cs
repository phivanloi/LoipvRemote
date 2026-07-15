using System;
using System.Windows.Forms;

namespace LoipvRemote.UI.Forms
{
    internal static class AppStartupLayout
    {
        internal const int DefaultSidebarWidth = 400;

        internal static int SidebarWidthForDpi(int dpi)
        {
            if (dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi));
            return (int)Math.Round(DefaultSidebarWidth * dpi / 96d);
        }

        internal static FormWindowState ResolveWindowState(bool startMinimized, bool startFullScreen)
        {
            if (startMinimized) return FormWindowState.Minimized;
            return startFullScreen ? FormWindowState.Normal : FormWindowState.Maximized;
        }
    }
}
