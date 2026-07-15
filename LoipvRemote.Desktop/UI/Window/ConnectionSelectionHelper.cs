using System.Collections.Generic;

using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Tree;

namespace LoipvRemote.UI.Window
{
    internal static class ConnectionSelectionHelper
    {
        internal static List<ConnectionInfo> GetDirectChildConnections(ConnectionInfo folder)
        {
            var directChildren = new List<ConnectionInfo>();
            if (folder is not ContainerInfo container)
                return directChildren;

            foreach (ConnectionInfo child in container.Children)
            {
                if (child.GetTreeNodeType() is TreeNodeType.Connection or TreeNodeType.PuttySession)
                    directChildren.Add(child);
            }

            return directChildren;
        }
    }
}
