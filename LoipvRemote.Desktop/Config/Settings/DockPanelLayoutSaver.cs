using System;
using System.IO;
using System.Runtime.Versioning;
using LoipvRemote.App.Info;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers;
using LoipvRemote.UI.Forms;
using LoipvRemote.Messages;
using WeifenLuo.WinFormsUI.Docking;

namespace LoipvRemote.Config.Settings
{
    [SupportedOSPlatform("windows")]
    public class DockPanelLayoutSaver
    {
        private readonly ISerializer<DockPanel, string> _dockPanelSerializer;
        private readonly IDataProvider<string> _dataProvider;
        private readonly MessageCollector _messageCollector;

        public DockPanelLayoutSaver(ISerializer<DockPanel, string> dockPanelSerializer,
                                    IDataProvider<string> dataProvider,
                                    MessageCollector messageCollector)
        {
            ArgumentNullException.ThrowIfNull(dockPanelSerializer);
            if (dataProvider == null)
                ArgumentNullException.ThrowIfNull(dataProvider);
            if (messageCollector == null)
                ArgumentNullException.ThrowIfNull(messageCollector);

            _dockPanelSerializer = dockPanelSerializer;
            _dataProvider = dataProvider;
            _messageCollector = messageCollector;
        }

        public void Save(FrmMain mainForm)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(mainForm);
                if (Directory.Exists(SettingsFileInfo.SettingsPath) == false)
                {
                    Directory.CreateDirectory(SettingsFileInfo.SettingsPath);
                }

                string serializedLayout = _dockPanelSerializer.Serialize(mainForm.pnlDock);
                _dataProvider.Save(serializedLayout);
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionStackTrace("SavePanelsToXML failed", ex);
            }
        }
    }
}
