using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers.ConnectionSerializers.Csv;
using LoipvRemote.Container;
using LoipvRemote.Messages;

namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    public sealed class LoipvRemoteCsvImporter(MessageCollector messageCollector) : IConnectionImporter<string>
    {
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));

        public void Import(string filePath, ContainerInfo destinationContainer)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                _messageCollector.AddMessage(MessageClass.ErrorMsg, "Unable to import file. File path is null.");
                return;
            }

            if (!File.Exists(filePath))
                _messageCollector.AddMessage(MessageClass.ErrorMsg,
                                                    $"Unable to import file. File does not exist. Path: {filePath}");

            FileDataProvider dataProvider = new(filePath);
            string xmlString = dataProvider.Load();
            CsvConnectionsDeserializer csvDeserializer = new();
            Tree.ConnectionTreeModel connectionTreeModel = csvDeserializer.Deserialize(xmlString);

            ContainerInfo rootImportContainer = new() { Name = Path.GetFileNameWithoutExtension(filePath) };
            rootImportContainer.AddChildRange(connectionTreeModel.RootNodes.First().Children.ToArray());
            destinationContainer.AddChild(rootImportContainer);
        }
    }
}
