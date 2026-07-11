using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.App;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers.ConnectionSerializers.Xml;
using LoipvRemote.Container;
using LoipvRemote.Messages;


namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    // ReSharper disable once InconsistentNaming
    public class LoipvRemoteXmlImporter : IConnectionImporter<string>
    {
        public void Import(string fileName, ContainerInfo destinationContainer)
        {
            if (fileName == null)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "Unable to import file. File path is null.");
                return;
            }

            if (!File.Exists(fileName))
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg,
                                                    $"Unable to import file. File does not exist. Path: {fileName}");

            FileDataProvider dataProvider = new(fileName);
            string xmlString = dataProvider.Load();
            XmlConnectionsDeserializer xmlConnectionsDeserializer = new();
            Tree.ConnectionTreeModel connectionTreeModel = xmlConnectionsDeserializer.Deserialize(xmlString, true);

            ContainerInfo rootImportContainer = new() { Name = Path.GetFileNameWithoutExtension(fileName)};
            rootImportContainer.AddChildRange(connectionTreeModel.RootNodes.First().Children.ToArray());
            destinationContainer.AddChild(rootImportContainer);
        }
    }
}