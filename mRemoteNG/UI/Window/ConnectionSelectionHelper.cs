using System.Collections.Generic;

using mRemoteNG.Connection;
using mRemoteNG.Container;
using mRemoteNG.Tree;

namespace mRemoteNG.UI.Window
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
