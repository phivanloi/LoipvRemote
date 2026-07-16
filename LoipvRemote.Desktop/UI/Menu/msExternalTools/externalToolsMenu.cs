using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LoipvRemote.UI.Window;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Panels;

namespace LoipvRemote.UI.Menu.msExternalTools
{
    public partial class externalToolsMenu : ToolStripMenuItem
    {
        public externalToolsMenu()
        {
            Initialize();
        }

        public externalToolsMenu(IContainer container)
        {
            container.Add(this);

            Initialize();
        }

        private static void Initialize()
        {
        }
    }
}
