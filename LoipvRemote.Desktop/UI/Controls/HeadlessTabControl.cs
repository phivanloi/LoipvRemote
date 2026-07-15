using LoipvRemote.Infrastructure.Windows.WindowMessages;
using System;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace LoipvRemote.UI.Controls
{
    [SupportedOSPlatform("windows")]
    public class HeadlessTabControl : TabControl
    {
        protected override void WndProc(ref Message m)
        {
            // Hide tabs by trapping the TCM_ADJUSTRECT message
            if (m.Msg == WindowsShellWindowMessages.TabControlAdjustRect && !DesignMode)
                m.Result = (IntPtr)1;
            else
                base.WndProc(ref m);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            //
            // HeadlessTabControl
            //
            this.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular,
                                                System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ResumeLayout(false);
        }
    }
}
