using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers.MiscSerializers;
using LoipvRemote.Container;


namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    public class PuttyConnectionManagerImporter : IConnectionImporter<string>
    {
        public void Import(string filePath, ContainerInfo destinationContainer)
        {
            FileDataProvider dataProvider = new(filePath);
            string xmlContent = dataProvider.Load();

            PuttyConnectionManagerDeserializer deserializer = new();
            Tree.ConnectionTreeModel connectionTreeModel = deserializer.Deserialize(xmlContent);

            ContainerInfo importedRootNode = connectionTreeModel.RootNodes.First();
            if (importedRootNode == null) return;
            Connection.ConnectionInfo[] childrenToAdd = importedRootNode.Children.ToArray();
            destinationContainer.AddChildRange(childrenToAdd);
        }
    }
}