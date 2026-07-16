using LoipvRemote.Tree.ClickHandlers;
using LoipvRemote.UI.Controls;
using LoipvRemote.UI.Controls.ConnectionTree;

namespace LoipvRemote.UI.Window
{
    public partial class ConnectionTreeWindow : BaseWindow
	{
        #region  Windows Form Designer generated code
		internal System.Windows.Forms.MenuStrip msMain;
		internal System.Windows.Forms.ToolStripMenuItem mMenViewExpandAllFolders;
		internal System.Windows.Forms.ToolStripMenuItem mMenViewCollapseAllFolders;
		internal System.Windows.Forms.ToolStripMenuItem mMenSort;
		internal System.Windows.Forms.ToolStripMenuItem mMenAddConnection;
		internal System.Windows.Forms.ToolStripMenuItem mMenAddFolder;
		public System.Windows.Forms.TreeView tvConnections;
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            LoipvRemote.Tree.ConnectionTreeModel connectionTreeModel2 = new LoipvRemote.Tree.ConnectionTreeModel();
            TreeNodeCompositeClickHandler treeNodeCompositeClickHandler3 = new TreeNodeCompositeClickHandler();
            LoipvRemote.Tree.AlwaysConfirmYes alwaysConfirmYes2 = new LoipvRemote.Tree.AlwaysConfirmYes();
            TreeNodeCompositeClickHandler treeNodeCompositeClickHandler4 = new TreeNodeCompositeClickHandler();
            this.ConnectionTree = new ConnectionTree();
            this.msMain = new System.Windows.Forms.MenuStrip();
            this.mMenAddConnection = new System.Windows.Forms.ToolStripMenuItem();
            this.mMenAddFolder = new System.Windows.Forms.ToolStripMenuItem();
            this.mMenViewExpandAllFolders = new System.Windows.Forms.ToolStripMenuItem();
            this.mMenViewCollapseAllFolders = new System.Windows.Forms.ToolStripMenuItem();
            this.mMenSort = new System.Windows.Forms.ToolStripMenuItem();
            this.mMenFavorites = new System.Windows.Forms.ToolStripMenuItem();
            this.vsToolStripExtender = new WeifenLuo.WinFormsUI.Docking.VisualStudioToolStripExtender(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.ConnectionTree)).BeginInit();
            this.msMain.SuspendLayout();
            this.SuspendLayout();
            //
            // olvConnections
            //
            this.ConnectionTree.AllowDrop = true;
            this.ConnectionTree.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.ConnectionTree.CellEditUseWholeCell = false;
            this.ConnectionTree.ConnectionTreeModel = connectionTreeModel2;
            this.ConnectionTree.Cursor = System.Windows.Forms.Cursors.Default;
            this.ConnectionTree.Dock = System.Windows.Forms.DockStyle.Fill;
            treeNodeCompositeClickHandler3.ClickHandlers = new ITreeNodeClickHandler<LoipvRemote.Connection.ConnectionInfo>[0];
            this.ConnectionTree.DoubleClickHandler = treeNodeCompositeClickHandler3;
            this.ConnectionTree.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ConnectionTree.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.ConnectionTree.HideSelection = false;
            this.ConnectionTree.IsSimpleDragSource = true;
            this.ConnectionTree.LabelEdit = true;
            this.ConnectionTree.Location = new System.Drawing.Point(0, 34);
            this.ConnectionTree.MultiSelect = true;
            this.ConnectionTree.Name = "ConnectionTree";
            this.ConnectionTree.NodeDeletionConfirmer = alwaysConfirmYes2;
            this.ConnectionTree.PostSetupActions = new IConnectionTreeAction[0];
            this.ConnectionTree.SelectedBackColor = System.Drawing.SystemColors.Highlight;
            this.ConnectionTree.SelectedForeColor = System.Drawing.SystemColors.HighlightText;
            this.ConnectionTree.ShowGroups = false;
            treeNodeCompositeClickHandler4.ClickHandlers = new ITreeNodeClickHandler<LoipvRemote.Connection.ConnectionInfo>[0];
            this.ConnectionTree.SingleClickHandler = treeNodeCompositeClickHandler4;
            this.ConnectionTree.Size = new System.Drawing.Size(204, 377);
            this.ConnectionTree.TabIndex = 20;
            this.ConnectionTree.UnfocusedSelectedBackColor = System.Drawing.SystemColors.Highlight;
            this.ConnectionTree.UnfocusedSelectedForeColor = System.Drawing.SystemColors.HighlightText;
            this.ConnectionTree.UseCompatibleStateImageBehavior = false;
            this.ConnectionTree.UseOverlays = false;
            this.ConnectionTree.View = System.Windows.Forms.View.Details;
            this.ConnectionTree.VirtualMode = true;
            //
            // msMain
            //
            this.msMain.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.msMain.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.msMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mMenAddConnection,
            this.mMenAddFolder,
            this.mMenViewExpandAllFolders,
            this.mMenViewCollapseAllFolders,
            this.mMenSort,
            this.mMenFavorites});
            this.msMain.Location = new System.Drawing.Point(0, 0);
            this.msMain.Name = "msMain";
            this.msMain.Padding = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this.msMain.ShowItemToolTips = true;
            this.msMain.Size = new System.Drawing.Size(204, 34);
            this.msMain.TabIndex = 10;
            this.msMain.Text = "MenuStrip1";
            //
            // mMenAddConnection
            //
            this.mMenAddConnection.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.mMenAddConnection.Image = global::LoipvRemote.Properties.Resources.AddItem_16x;
            this.mMenAddConnection.Name = "mMenAddConnection";
            this.mMenAddConnection.Padding = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.mMenAddConnection.Size = new System.Drawing.Size(24, 20);
            this.mMenAddConnection.Click += new System.EventHandler(this.CMenTreeAddConnection_Click);
            //
            // mMenAddFolder
            //
            this.mMenAddFolder.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.mMenAddFolder.Image = global::LoipvRemote.Properties.Resources.AddFolder_16x;
            this.mMenAddFolder.Name = "mMenAddFolder";
            this.mMenAddFolder.Size = new System.Drawing.Size(28, 20);
            this.mMenAddFolder.Click += new System.EventHandler(this.CMenTreeAddFolder_Click);
            //
            // mMenViewExpandAllFolders
            //
            this.mMenViewExpandAllFolders.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.mMenViewExpandAllFolders.Image = global::LoipvRemote.Properties.Resources.ExpandAll_16x;
            this.mMenViewExpandAllFolders.Name = "mMenViewExpandAllFolders";
            this.mMenViewExpandAllFolders.Size = new System.Drawing.Size(28, 20);
            this.mMenViewExpandAllFolders.Text = "Expand all folders";
            //
            // mMenViewCollapseAllFolders
            //
            this.mMenViewCollapseAllFolders.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.mMenViewCollapseAllFolders.Image = global::LoipvRemote.Properties.Resources.CollapseAll_16x;
            this.mMenViewCollapseAllFolders.Name = "mMenViewCollapseAllFolders";
            this.mMenViewCollapseAllFolders.Size = new System.Drawing.Size(28, 20);
            this.mMenViewCollapseAllFolders.Text = "Collapse all folders";
            //
            // mMenSortAscending
            //
            this.mMenSort.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.mMenSort.Image = global::LoipvRemote.Properties.Resources.SortAscending_16x;
            this.mMenSort.Name = "mMenSort";
            this.mMenSort.Size = new System.Drawing.Size(28, 20);
            //
            // mMenFavorites
            //
            this.mMenFavorites.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.mMenFavorites.Image = global::LoipvRemote.Properties.Resources.Favorite_16x;
            this.mMenFavorites.Name = "mMenFavorites";
            this.mMenFavorites.Size = new System.Drawing.Size(28, 20);
            this.mMenFavorites.Text = "Favorites";
            //
            // vsToolStripExtender
            //
            this.vsToolStripExtender.DefaultRenderer = null;
            //
            // ConnectionTreeWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(204, 411);
            this.Controls.Add(this.ConnectionTree);
            this.Controls.Add(this.msMain);
            this.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.HideOnClose = true;
            this.Name = "ConnectionTreeWindow";
            this.TabText = "Connections";
            this.Text = "Connections";
            this.Load += new System.EventHandler(this.Tree_Load);
            ((System.ComponentModel.ISupportInitialize)(this.ConnectionTree)).EndInit();
            this.msMain.ResumeLayout(false);
            this.msMain.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

		}
        #endregion

        private System.ComponentModel.IContainer components;
        private WeifenLuo.WinFormsUI.Docking.VisualStudioToolStripExtender vsToolStripExtender;
        internal System.Windows.Forms.ToolStripMenuItem mMenFavorites;
    }
}
