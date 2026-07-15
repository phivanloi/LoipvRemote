using LoipvRemote.UI.Controls.ConnectionTree;


namespace LoipvRemote.Tree
{
    public class RootNodeExpander : IConnectionTreeDelegate
    {
        public void Execute(IConnectionTree connectionTree)
        {
            Root.RootNodeInfo rootConnectionNode = connectionTree.GetRootConnectionNode();
            connectionTree.InvokeExpand(rootConnectionNode);
        }
    }
}