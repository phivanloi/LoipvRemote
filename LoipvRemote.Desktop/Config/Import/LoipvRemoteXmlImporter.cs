using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.Container;
using LoipvRemote.Infrastructure.Persistence.Xml;
using LoipvRemote.Messages;
using LoipvRemote.UI.Adapters;


namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    // ReSharper disable once InconsistentNaming
    public sealed class LoipvRemoteXmlImporter(MessageCollector messageCollector) : IConnectionImporter<string>
    {
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));

        public void Import(string fileName, ContainerInfo destinationContainer)
        {
            if (fileName == null)
            {
                _messageCollector.AddMessage(MessageClass.ErrorMsg, "Unable to import file. File path is null.");
                return;
            }

            if (!File.Exists(fileName))
            {
                _messageCollector.AddMessage(MessageClass.ErrorMsg,
                                                    $"Unable to import file. File does not exist. Path: {fileName}");
                return;
            }

            var store = new XmlConnectionDefinitionStore(fileName);
            Domain.Connections.ConnectionTreeDefinition definition = Task.Run(
                    () => store.LoadAsync(),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Tree.ConnectionTreeModel connectionTreeModel = ConnectionDefinitionMapper.ToDesktopTree(definition);

            ContainerInfo rootImportContainer = new() { Name = Path.GetFileNameWithoutExtension(fileName)};
            rootImportContainer.AddChildRange(connectionTreeModel.RootNodes.SelectMany(root => root.Children).ToArray());
            destinationContainer.AddChild(rootImportContainer);
        }
    }
}
