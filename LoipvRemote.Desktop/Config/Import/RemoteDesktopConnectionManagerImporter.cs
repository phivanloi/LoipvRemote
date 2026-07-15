using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers.MiscSerializers;
using LoipvRemote.Container;


namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    public class RemoteDesktopConnectionManagerImporter : IConnectionImporter<string>
    {
        public void Import(string filePath, ContainerInfo destinationContainer)
        {
            FileDataProvider dataProvider = new(filePath);
            string fileContent = dataProvider.Load();

            RemoteDesktopConnectionManagerDeserializer deserializer = new();
            Tree.ConnectionTreeModel connectionTreeModel = deserializer.Deserialize(fileContent);

            ContainerInfo importedRootNode = connectionTreeModel.RootNodes.First();
            if (importedRootNode == null) return;
            Connection.ConnectionInfo[] childrenToAdd = importedRootNode.Children.ToArray();
            destinationContainer.AddChildRange(childrenToAdd);
        }
    }
}