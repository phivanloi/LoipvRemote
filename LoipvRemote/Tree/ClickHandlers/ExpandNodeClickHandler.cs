using System;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.UI.Controls.ConnectionTree;

namespace LoipvRemote.Tree.ClickHandlers
{
    public class ExpandNodeClickHandler : ITreeNodeClickHandler<ConnectionInfo>
    {
        private readonly IConnectionTree _connectionTree;

        public ExpandNodeClickHandler(IConnectionTree connectionTree)
        {
            if (connectionTree == null)
                throw new ArgumentNullException(nameof(connectionTree));

            _connectionTree = connectionTree;
        }

        public void Execute(ConnectionInfo clickedNode)
        {
            ContainerInfo? clickedNodeAsContainer = clickedNode as ContainerInfo;
            if (clickedNodeAsContainer == null) return;
            _connectionTree.ToggleExpansion(clickedNodeAsContainer);
        }
    }
}