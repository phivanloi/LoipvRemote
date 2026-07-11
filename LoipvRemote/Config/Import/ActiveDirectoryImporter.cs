using System;
using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.App;
using LoipvRemote.Config.Serializers.MiscSerializers;
using LoipvRemote.Container;
using LoipvRemote.Tools;

namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    public class ActiveDirectoryImporter : IConnectionImporter<string>
    {
        public void Import(string ldapPath, ContainerInfo destinationContainer)
        {
            Import(ldapPath, destinationContainer, false);
        }

        public static void Import(string ldapPath, ContainerInfo destinationContainer, bool importSubOu)
        {
            try
            {
                ldapPath.ThrowIfNullOrEmpty(nameof(ldapPath));
                ActiveDirectoryDeserializer deserializer = new(ldapPath, importSubOu);
                Tree.ConnectionTreeModel connectionTreeModel = deserializer.Deserialize();
                ContainerInfo importedRootNode = connectionTreeModel.RootNodes.First();
                if (importedRootNode == null) return;
                Connection.ConnectionInfo[] childrenToAdd = importedRootNode.Children.ToArray();
                destinationContainer.AddChildRange(childrenToAdd);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Config.Import.ActiveDirectory.Import() failed.", ex);
            }
        }
    }
}