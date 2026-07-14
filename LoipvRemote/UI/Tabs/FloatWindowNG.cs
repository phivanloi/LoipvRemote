using System;
using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using LoipvRemote.Themes;
using LoipvRemote.Infrastructure.Windows.Interop;

namespace LoipvRemote.UI.Tabs
{
    class FloatWindowNG : FloatWindow
    {
        public FloatWindowNG(DockPanel dockPanel, DockPane pane)
            : base(dockPanel, pane)
        {
            setDefaultProperties();
        }

        public FloatWindowNG(DockPanel dockPanel, DockPane pane, Rectangle bounds)
            : base(dockPanel, pane, bounds)
        {
            setDefaultProperties();
        }

        private void setDefaultProperties()
        {
            FormBorderStyle = FormBorderStyle.Sizable;

            // To enable Alt+Tab between your undocked forms and your main form
            ShowInTaskbar = true;
            Owner = null;

            // Allow the Windows default behavior of maximizing/restoring the window
            DoubleClickTitleBarToDock = true;
        }

        // Apply the dark/light title bar before the window is shown to avoid a white flash.
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ThemeManager.getInstance().ApplyThemeToTitleBar(this);
        }

        //[SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        protected override void WndProc(ref Message m)
        {
            int WM_NCLBUTTONDOWN = 0x00A1;
            int WM_SYSCOMMAND = 0x0112;

            int SC_MINIMIZE = 0xF020;
            int SC_RESTORE = 0xF120;

            if (m.Msg == WM_NCLBUTTONDOWN)
            {
                if (IsDisposed)
                    return;

                if ((uint)m.WParam == 8) // Check if button down occured in minimize box
                {
                    if (WindowState == FormWindowState.Minimized)
                        _ = NativeMethods.SendMessage(Handle, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero);
                    else
                        _ = NativeMethods.SendMessage(Handle, WM_SYSCOMMAND, (IntPtr)SC_MINIMIZE, IntPtr.Zero);

                    return;
                }
            }

            base.WndProc(ref m);
        }
    }

    public class CustomFloatWindowFactory : DockPanelExtender.IFloatWindowFactory
    {
        public FloatWindow CreateFloatWindow(DockPanel dockPanel, DockPane pane, Rectangle bounds)
        {
            return new FloatWindowNG(dockPanel, pane, bounds);
        }

        public FloatWindow CreateFloatWindow(DockPanel dockPanel, DockPane pane)
        {
            return new FloatWindowNG(dockPanel, pane);
        }
    }
}
