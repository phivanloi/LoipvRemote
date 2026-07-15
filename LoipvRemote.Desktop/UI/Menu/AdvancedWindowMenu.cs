using System;
using System.Windows.Forms;
using LoipvRemote.Infrastructure.Windows.WindowMenus;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;

namespace LoipvRemote.UI.Menu
{
    [SupportedOSPlatform("windows")]
    // This class creates new menu items to menu that appears when you right click the top of the app (where the window title is)
    public class AdvancedWindowMenu(IWin32Window boundControl) : IDisposable
    {
        private readonly WindowsSystemMenu _windowMenu = new(boundControl.Handle);
        private readonly int[] _sysMenSubItems = new int[51];

        public Screen GetScreenById(int id)
        {
            for (int i = 0; i <= _sysMenSubItems.Length - 1; i++)
            {
                if (_sysMenSubItems[i] != id) continue;
                return Screen.AllScreens[i];
            }

            return null;
        }

        public void OnDisplayChanged(object sender, EventArgs e)
        {
            ResetScreenList();
            BuildAdditionalMenuItems();
        }

        private void ResetScreenList()
        {
            _windowMenu.Reset();
        }

        public void BuildAdditionalMenuItems()
        {
            // option to send main form to another screen
            IntPtr popMen = _windowMenu.CreatePopupMenuItem();
            for (int i = 0; i <= Screen.AllScreens.Length - 1; i++)
            {
                _sysMenSubItems[i] = 200 + i;
                _windowMenu.AppendMenuItem(popMen, WindowsSystemMenu.Flags.String, new IntPtr(_sysMenSubItems[i]),
                                           Language.Screen + " " + Convert.ToString(i + 1));
            }
            _windowMenu.InsertMenuItem(_windowMenu.SystemMenuHandle, 0,
                WindowsSystemMenu.Flags.Popup | WindowsSystemMenu.Flags.ByPosition, popMen,
                Language.SendTo);
            // separator
            _windowMenu.InsertMenuItem(_windowMenu.SystemMenuHandle, 1,
                                       WindowsSystemMenu.Flags.ByPosition | WindowsSystemMenu.Flags.Separator, IntPtr.Zero,
                                       null);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;

            _windowMenu?.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
