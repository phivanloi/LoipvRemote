using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.Container;
using LoipvRemote.UI.Controls.ConnectionTree;


namespace LoipvRemote.Tree
{
    [SupportedOSPlatform("windows")]
    public class PreviouslyOpenedFolderExpander : IConnectionTreeAction
    {
        public void Execute(IConnectionTree connectionTree)
        {
            Root.RootNodeInfo rootNode = connectionTree.GetRootConnectionNode();
            System.Collections.Generic.IEnumerable<ContainerInfo> containerList = ConnectionTreeModel.GetRecursiveChildList(rootNode)
                                              .OfType<ContainerInfo>();
            System.Collections.Generic.IEnumerable<ContainerInfo> previouslyExpandedNodes = containerList.Where(container => container.IsExpanded);
            connectionTree.ExpandedObjects = previouslyExpandedNodes;
            connectionTree.InvokeRebuildAll(true);
        }
    }
}
