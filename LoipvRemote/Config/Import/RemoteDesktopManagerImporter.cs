#region

using System.IO;
using System.Runtime.Versioning;
using LoipvRemote.App;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers.ConnectionSerializers.Csv.RemoteDesktopManager;
using LoipvRemote.Container;
using LoipvRemote.Messages;

#endregion

namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    public class RemoteDesktopManagerImporter : IConnectionImporter<string>
    {
        public void Import(string filePath, ContainerInfo destinationContainer)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, $"Unable to import file. File path is null.");
                return;
            }

            if (!File.Exists(filePath))
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, $"Unable to import file. File does not exist. Path: {filePath}");

            FileDataProvider dataProvider = new(filePath);
            string csvString = dataProvider.Load();

            if (!string.IsNullOrEmpty(csvString))
            {
                CsvConnectionsDeserializerRdmFormat csvDeserializer = new();
                Tree.ConnectionTreeModel connectionTreeModel = csvDeserializer.Deserialize(csvString);

                ContainerInfo rootContainer = new() { Name = Path.GetFileNameWithoutExtension(filePath) };
                rootContainer.AddChildRange(connectionTreeModel.RootNodes);
                destinationContainer.AddChild(rootContainer);
            }
            else
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, "Unable to import file. File is empty.");
                return;
            }
        }
    }
}