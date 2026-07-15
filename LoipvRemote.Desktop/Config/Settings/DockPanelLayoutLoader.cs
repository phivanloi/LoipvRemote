using LoipvRemote.App;
using LoipvRemote.App.Info;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Window;
using System;
using System.IO;
using LoipvRemote.Messages;
using WeifenLuo.WinFormsUI.Docking;
using System.Runtime.Versioning;

namespace LoipvRemote.Config.Settings
{
    [SupportedOSPlatform("windows")]
    public class DockPanelLayoutLoader
    {
        private readonly FrmMain _mainForm;
        private readonly MessageCollector _messageCollector;

        public DockPanelLayoutLoader(FrmMain mainForm, MessageCollector messageCollector)
        {
            ArgumentNullException.ThrowIfNull(mainForm);
            ArgumentNullException.ThrowIfNull(messageCollector);

            _mainForm = mainForm;
            _messageCollector = messageCollector;
        }

        public void LoadPanelsFromXml()
        {
            try
            {
                while (_mainForm.pnlDock.Contents.Count > 0)
                {
                    DockContent dc = (DockContent)_mainForm.pnlDock.Contents[0];
                    dc.Close();
                }

#if !PORTABLE
                string oldPath =
 Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\" + GeneralAppInfo.ProductName + "\\" + SettingsFileInfo.LayoutFileName;
#endif
                string newPath = SettingsFileInfo.SettingsPath + "\\" + SettingsFileInfo.LayoutFileName;
                if (File.Exists(newPath))
                {
                    _mainForm.pnlDock.LoadFromXml(newPath, GetContentFromPersistString);
#if !PORTABLE
				}
				else if (File.Exists(oldPath))
				{
					_mainForm.pnlDock.LoadFromXml(oldPath, GetContentFromPersistString);
#endif
                }
                else
                {
                    _mainForm.SetDefaultLayout();
                }
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("LoadPanelsFromXML failed", ex);
            }
        }

        private IDockContent? GetContentFromPersistString(string persistString)
        {
            try
            {
                if (persistString == typeof(ConfigWindow).ToString())
                    return AppWindows.ConfigForm;

                if (persistString == typeof(ConnectionTreeWindow).ToString())
                    return AppWindows.TreeForm;

            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("GetContentFromPersistString failed", ex);
            }

            return null;
        }
    }
}
