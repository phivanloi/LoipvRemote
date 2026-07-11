using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers.MiscSerializers;
using LoipvRemote.Container;


namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    public class RemoteDesktopConnectionImporter : IConnectionImporter<string>
    {
        public void Import(string fileName, ContainerInfo destinationContainer)
        {
            FileDataProvider dataProvider = new(fileName);
            string content = dataProvider.Load();

            RemoteDesktopConnectionDeserializer deserializer = new();
            Tree.ConnectionTreeModel connectionTreeModel = deserializer.Deserialize(content);

            Connection.ConnectionInfo importedConnection = connectionTreeModel.RootNodes.First().Children.First();

            if (importedConnection == null) return;
            importedConnection.Name = Path.GetFileNameWithoutExtension(fileName);
            destinationContainer.AddChild(importedConnection);
        }
    }
}