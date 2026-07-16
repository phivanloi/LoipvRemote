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
        private readonly DesktopWindowCatalog _windows;

        public DockPanelLayoutLoader(FrmMain mainForm, MessageCollector messageCollector, DesktopWindowCatalog windows)
        {
            ArgumentNullException.ThrowIfNull(mainForm);
            ArgumentNullException.ThrowIfNull(messageCollector);
            ArgumentNullException.ThrowIfNull(windows);

            _mainForm = mainForm;
            _messageCollector = messageCollector;
            _windows = windows;
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

                EnsurePrimarySidebarPanels();
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
                    return _windows.ConfigForm;

                if (persistString == typeof(ConnectionTreeWindow).ToString())
                    return _windows.TreeForm;

            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("GetContentFromPersistString failed", ex);
            }

            return null;
        }

        /// <summary>
        /// Repairs layouts written by older builds or by a closed/hidden panel.
        /// Connections and Config are the shell's primary navigation panels and
        /// must remain available in the left sidebar after startup.
        /// </summary>
        public void EnsurePrimarySidebarPanels()
        {
            EnsurePanelVisible(_windows.ConfigForm);
            EnsurePanelVisible(_windows.TreeForm);
        }

        private void EnsurePanelVisible(DockContent panel)
        {
            if (panel.IsDisposed)
                return;

            if (panel.DockState is DockState.Hidden or DockState.Unknown || !panel.Visible)
                panel.Show(_mainForm.pnlDock, DockState.DockLeft);
        }
    }
}
