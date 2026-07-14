using System;
using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.Config.Serializers.MiscSerializers;
using LoipvRemote.Container;
using LoipvRemote.Tools;
using LoipvRemote.Messages;

namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    public sealed class ActiveDirectoryImporter(MessageCollector messageCollector) : IConnectionImporter<string>
    {
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));

        public void Import(string ldapPath, ContainerInfo destinationContainer)
        {
            Import(ldapPath, destinationContainer, false);
        }

        public void Import(string ldapPath, ContainerInfo destinationContainer, bool importSubOu)
        {
            try
            {
                ldapPath.ThrowIfNullOrEmpty(nameof(ldapPath));
                ActiveDirectoryDeserializer deserializer = new(ldapPath, importSubOu, _messageCollector);
                Tree.ConnectionTreeModel connectionTreeModel = deserializer.Deserialize();
                ContainerInfo importedRootNode = connectionTreeModel.RootNodes.First();
                if (importedRootNode == null) return;
                Connection.ConnectionInfo[] childrenToAdd = importedRootNode.Children.ToArray();
                destinationContainer.AddChildRange(childrenToAdd);
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("Config.Import.ActiveDirectory.Import() failed.", ex);
            }
        }
    }
}
