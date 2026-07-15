using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Xml;
using LoipvRemote.App.Info;
using LoipvRemote.Messages;
using LoipvRemote.Tools;

namespace LoipvRemote.Config.Settings
{
    [SupportedOSPlatform("windows")]
    public sealed class ExternalAppsSaver(MessageCollector messageCollector)
    {
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));

        public void Save(IEnumerable<ExternalTool> externalTools)
        {
            try
            {
                if (Directory.Exists(SettingsFileInfo.SettingsPath) == false)
                {
                    Directory.CreateDirectory(SettingsFileInfo.SettingsPath);
                }

                XmlTextWriter xmlTextWriter =
                    new(SettingsFileInfo.SettingsPath + "\\" + SettingsFileInfo.ExtAppsFilesName,
                                      Encoding.UTF8)
                    {
                        Formatting = Formatting.Indented,
                        Indentation = 4
                    };

                xmlTextWriter.WriteStartDocument();
                xmlTextWriter.WriteStartElement("Apps");

                foreach (ExternalTool extA in externalTools)
                {
                    xmlTextWriter.WriteStartElement("App");
                    xmlTextWriter.WriteAttributeString("DisplayName", "", extA.DisplayName);
                    xmlTextWriter.WriteAttributeString("FileName", "", extA.FileName);
                    xmlTextWriter.WriteAttributeString("Arguments", "", extA.Arguments);
                    xmlTextWriter.WriteAttributeString("WorkingDir", "", extA.WorkingDir);
                    xmlTextWriter.WriteAttributeString("WaitForExit", "", Convert.ToString(extA.WaitForExit));
                    xmlTextWriter.WriteAttributeString("TryToIntegrate", "", Convert.ToString(extA.TryIntegrate));
                    xmlTextWriter.WriteAttributeString("RunElevated", "", Convert.ToString(extA.RunElevated));
                    xmlTextWriter.WriteAttributeString("ShowOnToolbar", "", Convert.ToString(extA.ShowOnToolbar));
                    xmlTextWriter.WriteEndElement();
                }

                xmlTextWriter.WriteEndElement();
                xmlTextWriter.WriteEndDocument();

                xmlTextWriter.Close();
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionStackTrace("SaveExternalAppsToXML failed", ex);
            }
        }
    }
}
