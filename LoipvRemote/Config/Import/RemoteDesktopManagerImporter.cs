#region

using System.IO;
using System.Runtime.Versioning;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers.ConnectionSerializers.Csv.RemoteDesktopManager;
using LoipvRemote.Container;
using LoipvRemote.Messages;

#endregion

namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    public sealed class RemoteDesktopManagerImporter(MessageCollector messageCollector) : IConnectionImporter<string>
    {
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));

        public void Import(string filePath, ContainerInfo destinationContainer)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                _messageCollector.AddMessage(MessageClass.ErrorMsg, $"Unable to import file. File path is null.");
                return;
            }

            if (!File.Exists(filePath))
                _messageCollector.AddMessage(MessageClass.ErrorMsg, $"Unable to import file. File does not exist. Path: {filePath}");

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
                _messageCollector.AddMessage(MessageClass.ErrorMsg, "Unable to import file. File is empty.");
                return;
            }
        }
    }
}
