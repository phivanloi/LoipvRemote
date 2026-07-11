using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.Config.Serializers.MiscSerializers;
using LoipvRemote.Connection.Protocol;
using LoipvRemote.Container;
using LoipvRemote.Tools;


namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    public class PortScanImporter(ProtocolType targetProtocolType) : IConnectionImporter<IEnumerable<ScanHost>>
    {
        private readonly ProtocolType _targetProtocolType = targetProtocolType;

        public void Import(IEnumerable<ScanHost> hosts, ContainerInfo destinationContainer)
        {
            PortScanDeserializer deserializer = new(_targetProtocolType);
            Tree.ConnectionTreeModel connectionTreeModel = deserializer.Deserialize(hosts);

            ContainerInfo importedRootNode = connectionTreeModel.RootNodes.First();
            if (importedRootNode == null) return;
            Connection.ConnectionInfo[] childrenToAdd = importedRootNode.Children.ToArray();
            destinationContainer.AddChildRange(childrenToAdd);
        }
    }
}