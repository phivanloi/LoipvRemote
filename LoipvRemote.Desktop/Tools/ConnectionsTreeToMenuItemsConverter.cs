using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Tree;
using LoipvRemote.Messages;


namespace LoipvRemote.Tools
{
    [SupportedOSPlatform("windows")]
    public sealed class ConnectionsTreeToMenuItemsConverter(MessageCollector messageCollector)
    {
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
        public MouseEventHandler? MouseUpEventHandler { get; set; }


        public IEnumerable<ToolStripDropDownItem> CreateToolStripDropDownItems(ConnectionTreeModel connectionTreeModel)
        {
            List<ContainerInfo> rootNodes = connectionTreeModel.RootNodes;
            return CreateToolStripDropDownItems(rootNodes);
        }

        public IEnumerable<ToolStripDropDownItem> CreateToolStripDropDownItems(IEnumerable<ConnectionInfo> nodes)
        {
            List<ToolStripDropDownItem> dropDownList = new();
            try
            {
                dropDownList.AddRange(nodes.Select(CreateMenuItem));
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("frmMain.AddNodeToMenu() failed", ex);
            }

            return dropDownList;
        }

        private void AddSubMenuNodes(IEnumerable<ConnectionInfo> nodes, ToolStripDropDownItem toolStripMenuItem)
        {
            foreach (ConnectionInfo connectionInfo in nodes)
            {
                ToolStripDropDownItem newItem = CreateMenuItem(connectionInfo);
                toolStripMenuItem.DropDownItems.Add(newItem);
            }
        }

        private ToolStripDropDownItem CreateMenuItem(ConnectionInfo node)
        {
            ToolStripMenuItem menuItem = new()
            {
                Text = node.Name,
                Tag = node
            };

            ContainerInfo nodeAsContainer = node as ContainerInfo;
            if (nodeAsContainer != null)
            {
                menuItem.Image = Properties.Resources.FolderClosed_16x;
                menuItem.Tag = nodeAsContainer;
                AddSubMenuNodes(nodeAsContainer.Children, menuItem);
            }
            else if (node.GetTreeNodeType() == TreeNodeType.PuttySession)
            {
                menuItem.Image = Properties.Resources.PuttySessions;
                menuItem.Tag = node;
            }
            else if (node.GetTreeNodeType() == TreeNodeType.Connection)
            {
                menuItem.Image = node.OpenConnections.Count > 0 ? Properties.Resources.Run_16x : Properties.Resources.Stop_16x;
                menuItem.Tag = node;
            }

            if (MouseUpEventHandler is { } mouseUpEventHandler)
                menuItem.MouseUp += mouseUpEventHandler;
            return menuItem;
        }
    }
}
