using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace LoipvRemote.UI
{
    [SupportedOSPlatform("windows")]
    public class FontOverrider
    {
        public static void FontOverride(Control ctlParent)
        {
            // Override the font of all controls in a container with the default font based on the OS version
            foreach (Control tempLoopVarCtlChild in ctlParent.Controls)
            {
                Control ctlChild = tempLoopVarCtlChild;
                // Only create a new Font if the font name is different to avoid unnecessary GDI operations
                Font? currentFont = ctlChild.Font;
                Font messageFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
                if (currentFont is not null && currentFont.Name != messageFont.Name)
                {
                    ctlChild.Font = new Font(messageFont.Name, currentFont.Size, currentFont.Style,
                                             currentFont.Unit, currentFont.GdiCharSet);
                }
                if (ctlChild.Controls.Count > 0)
                {
                    FontOverride(ctlChild);
                }
            }
        }
    }
}
