using System.Collections;
using LoipvRemote.Connection;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;

namespace LoipvRemote.UI.Controls.ConnectionTree
{
    public interface IConnectionTree
    {
        ConnectionTreeModel ConnectionTreeModel { get; set; }

        ConnectionInfo SelectedNode { get; }

        IEnumerable ExpandedObjects { get; set; }

        RootNodeInfo GetRootConnectionNode();

        void InvokeExpand(object model);

        void InvokeRebuildAll(bool preserveState);

        void ToggleExpansion(object model);
    }
}