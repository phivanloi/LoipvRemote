using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LoipvRemote.App;
using LoipvRemote.App.Composition;
using LoipvRemote.Config.Connections;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Properties;
using LoipvRemote.Themes;
using LoipvRemote.Tree;
using LoipvRemote.Tree.ClickHandlers;
using LoipvRemote.Tree.Root;
using LoipvRemote.UI.Controls.ConnectionTree;
using LoipvRemote.UI.TaskDialog;
using WeifenLuo.WinFormsUI.Docking;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;
using LoipvRemote.UI.DesignSystem;

// ReSharper disable ArrangeAccessorOwnerBody

namespace LoipvRemote.UI.Window
{
    [SupportedOSPlatform("windows")]
    public partial class ConnectionTreeWindow
    {
        private ThemeManager _themeManager = null!;
        private bool _sortedAz = true;
        private DesktopShellRuntime? _desktopShellRuntime;
        private bool _runtimeHandlersAttached;

        public ConnectionInfo SelectedNode => ConnectionTree.SelectedNode;

        public ConnectionTree ConnectionTree { get; set; } = null!;
        public ConnectionTreeWindow() : this(new DockContent())
        {
        }

        public ConnectionTreeWindow(DockContent panel)
        {
            WindowType = WindowType.Tree;
            DockPnl = panel;
            Icon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.ASPWebSite_16x);
            InitializeComponent();
            UiScaleManager.Instance.Apply(this);
            UiScaleManager.Instance.ApplyToolStrip(msMain);
            ApplyToolbarLayout();
            msMain.Paint += SidebarToolbarOnPaint;
            UiScaleManager.Instance.Changed += UiScaleManagerOnChanged;
            Disposed += (_, _) =>
            {
                UiScaleManager.Instance.Changed -= UiScaleManagerOnChanged;
                if (_runtimeHandlersAttached)
                    ShellRuntime.ConnectionTreeWorkspace.ConnectionsLoaded -= WorkspaceOnConnectionsLoaded;
            };
            SetMenuEventHandlers();
            SetConnectionTreeEventHandlers();
            Settings.Default.PropertyChanged += OnAppSettingsChanged;
            ApplyLanguage();
        }

        internal void AttachRuntime(DesktopShellRuntime desktopShellRuntime)
        {
            ArgumentNullException.ThrowIfNull(desktopShellRuntime);
            if (_desktopShellRuntime is not null && !ReferenceEquals(_desktopShellRuntime, desktopShellRuntime))
                throw new InvalidOperationException("The connection-tree runtime is already attached.");

            _desktopShellRuntime = desktopShellRuntime;
            if (_runtimeHandlersAttached)
                return;

            ConnectionTree.AttachRuntime(desktopShellRuntime);
            SetTreePostSetupActions();
            SetConnectionTreeClickHandlers();
            ShellRuntime.ConnectionTreeWorkspace.ConnectionsLoaded += WorkspaceOnConnectionsLoaded;
            _runtimeHandlersAttached = true;
        }

        private DesktopShellRuntime ShellRuntime => _desktopShellRuntime
            ?? throw new InvalidOperationException("The connection-tree runtime must be attached before it handles commands.");

        private void OnAppSettingsChanged(object o, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName == nameof(Settings.SlowClickRenameEnabled))
                ConnectionTree.SetupSlowClickRename();

            if (_runtimeHandlersAttached)
                SetConnectionTreeClickHandlers();
        }

        private void UiScaleManagerOnChanged(object? sender, EventArgs e)
        {
            ApplyToolbarLayout();
        }

        private void ApplyToolbarLayout()
        {
            UiMetrics metrics = UiScaleManager.Instance.Metrics;
            SidebarToolbarLayout layout = SidebarToolbarMetrics.ForDpi(
                msMain.DeviceDpi,
                metrics.IconSize,
                metrics.InteractiveHeight,
                metrics.IconHitTarget);

            msMain.AutoSize = false;
            msMain.Height = layout.Height;
            msMain.Padding = new Padding(layout.HorizontalPadding, 0, layout.HorizontalPadding, 0);
            msMain.ImageScalingSize = new System.Drawing.Size(layout.IconSize, layout.IconSize);

            foreach (ToolStripItem item in msMain.Items)
            {
                item.AutoSize = false;
                item.Margin = Padding.Empty;
                item.Padding = Padding.Empty;
                item.Size = new System.Drawing.Size(layout.ItemWidth, layout.Height);
                item.ImageAlign = System.Drawing.ContentAlignment.MiddleCenter;
            }
        }

        private void SidebarToolbarOnPaint(object? sender, PaintEventArgs e)
        {
            if (msMain.ClientSize.Width <= 0 || msMain.ClientSize.Height <= 0) return;

            using Pen border = new(Color.FromArgb(214, 219, 229));
            int right = msMain.ClientSize.Width - 1;
            int bottom = msMain.ClientSize.Height - 1;
            e.Graphics.DrawLine(border, 0, 0, right, 0);
            e.Graphics.DrawLine(border, 0, 0, 0, bottom);
            e.Graphics.DrawLine(border, right, 0, right, bottom);
        }


        #region Form Stuff

        private void Tree_Load(object? sender, EventArgs e)
        {
            //work on the theme change
            _themeManager = ThemeManager.getInstance();
            _themeManager.ThemeChanged += ApplyTheme;
            ApplyTheme();

        }

        private void ApplyLanguage()
        {
            Text = Language.Connections;
            TabText = Language.Connections;

            mMenAddConnection.ToolTipText = Language.NewConnection;
            mMenAddFolder.ToolTipText = Language.NewFolder;
            mMenViewExpandAllFolders.ToolTipText = Language.ExpandAllFolders;
            mMenViewCollapseAllFolders.ToolTipText = Language.CollapseAllFolders;
            mMenSort.ToolTipText = Language.Sort;
            mMenFavorites.ToolTipText = Language.Favorites;

        }

        private new void ApplyTheme()
        {
            if (!_themeManager.ThemingActive)
                return;

            ThemeInfo activeTheme = _themeManager.ActiveTheme;
            vsToolStripExtender.SetStyle(msMain, activeTheme.Version, activeTheme.Theme);
            vsToolStripExtender.SetStyle(ConnectionTree.ContextMenuStrip, activeTheme.Version,
                activeTheme.Theme);

            if (!_themeManager.ActiveAndExtended)
                return;

        }

        #endregion

        #region ConnectionTree

        private void SetConnectionTreeEventHandlers()
        {
            ConnectionTree.NodeDeletionConfirmer =
                new SelectedConnectionDeletionConfirmer(prompt => CTaskDialog.MessageBox(
                    Application.ProductName, prompt, "", ETaskDialogButtons.YesNo, ESysIcons.Question));
            ConnectionTree.KeyDown += TvConnections_KeyDown;
        }

        private void SetTreePostSetupActions()
        {
            List<IConnectionTreeDelegate> actions = new()
            {
                new PreviouslyOpenedFolderExpander(),
                new RootNodeExpander()
            };

            if (Properties.OptionsStartupExitPage.Default.OpenConsFromLastSession && !Properties.OptionsAdvancedPage.Default.NoReconnect)
                actions.Add(new PreviousSessionOpener(ShellRuntime.ConnectionInitiator));

            ConnectionTree.PostSetupActions = actions;
        }

        private void SetConnectionTreeClickHandlers()
        {
            List<ITreeNodeClickHandler<ConnectionInfo>> singleClickHandlers = new();
            List<ITreeNodeClickHandler<ConnectionInfo>> doubleClickHandlers = new()
            {
                new ExpandNodeClickHandler(ConnectionTree)
            };

            if (Settings.Default.SingleClickOnConnectionOpensIt)
                singleClickHandlers.Add(new OpenConnectionClickHandler(ShellRuntime.ConnectionInitiator));
            else
                doubleClickHandlers.Add(new OpenConnectionClickHandler(ShellRuntime.ConnectionInitiator));

            if (Settings.Default.SingleClickSwitchesToOpenConnection)
                singleClickHandlers.Add(new SwitchToConnectionClickHandler(ShellRuntime.ConnectionInitiator));

            ConnectionTree.SingleClickHandler = new TreeNodeCompositeClickHandler { ClickHandlers = singleClickHandlers };
            ConnectionTree.DoubleClickHandler = new TreeNodeCompositeClickHandler { ClickHandlers = doubleClickHandlers };
        }

        private void WorkspaceOnConnectionsLoaded(object o, ConnectionsLoadedEventArgs connectionsLoadedEventArgs)
        {
            if (ConnectionTree.InvokeRequired)
            {
                ConnectionTree.Invoke(() => WorkspaceOnConnectionsLoaded(o, connectionsLoadedEventArgs));
                return;
            }

            ConnectionTree.ConnectionTreeModel = connectionsLoadedEventArgs.NewConnectionTreeModel;
            ConnectionTree.SelectedObject = connectionsLoadedEventArgs.NewConnectionTreeModel.RootNodes.FirstOrDefault();
        }

        #endregion

        #region Top Menu

        private void SetMenuEventHandlers()
        {
            mMenViewExpandAllFolders.Click += (sender, args) => ConnectionTree.ExpandAll();
            mMenViewCollapseAllFolders.Click += (sender, args) =>
            {
                ConnectionTree.CollapseAll();
                ConnectionTree.Expand(ConnectionTree.GetRootConnectionNode());
            };
            mMenSort.Click += (sender, args) =>
            {
                if (_sortedAz)
                {
                    ConnectionTree.SortRecursive(ConnectionTree.GetRootConnectionNode(), ListSortDirection.Ascending);
                    mMenSort.Image = Properties.Resources.SortDescending_16x;
                    _sortedAz = false;
                }
                else
                {
                    ConnectionTree.SortRecursive(ConnectionTree.GetRootConnectionNode(), ListSortDirection.Descending);
                    mMenSort.Image = Properties.Resources.SortAscending_16x;
                    _sortedAz = true;
                }
            };
            mMenFavorites.Click += (sender, args) =>
            {
                mMenFavorites.DropDownItems.Clear();
                List<ContainerInfo> rootNodes = ShellRuntime.ConnectionTreeWorkspace.ConnectionTreeModel.RootNodes;
                List<ToolStripMenuItem> favoritesList = new();

                foreach (ContainerInfo node in rootNodes)
                {
                    foreach (ConnectionInfo containerInfo in ShellRuntime.ConnectionTreeWorkspace.ConnectionTreeModel.GetRecursiveFavoriteChildList(node))
                    {
                        ToolStripMenuItem favoriteMenuItem = new()
                        {
                            Text = containerInfo.Name,
                            Tag = containerInfo,
                            Image = containerInfo.OpenConnections.Count > 0 ? Properties.Resources.Run_16x : Properties.Resources.Stop_16x
                        };
                        favoriteMenuItem.MouseUp += FavoriteMenuItem_MouseUp;
                        favoritesList.Add(favoriteMenuItem);
                    }
                }

                mMenFavorites.DropDownItems.AddRange(favoritesList.ToArray());
                mMenFavorites.ShowDropDown();
            };
        }

        private void FavoriteMenuItem_MouseUp(object? sender, MouseEventArgs e)
        {
            if (((ToolStripMenuItem)sender).Tag is ContainerInfo) return;
            ShellRuntime.ConnectionInitiator.OpenConnection((ConnectionInfo)((ToolStripMenuItem)sender).Tag);
        }

        #endregion

        #region Tree Context Menu

        private void CMenTreeAddConnection_Click(object? sender, EventArgs e)
        {
            ConnectionTree.AddConnection();
        }

        private void CMenTreeAddFolder_Click(object? sender, EventArgs e)
        {
            ConnectionTree.AddFolder();
        }

        #endregion

        #region Tree Navigation

        public void JumpToNode(ConnectionInfo? connectionInfo)
        {
            if (connectionInfo == null)
            {
                ConnectionTree.SelectedObject = null;
                return;
            }

            ExpandParentsRecursive(connectionInfo);
            ConnectionTree.SelectObject(connectionInfo);
            ConnectionTree.EnsureModelVisible(connectionInfo);
        }

        private void ExpandParentsRecursive(ConnectionInfo connectionInfo)
        {
            while (true)
            {
                if (connectionInfo?.Parent == null) return;
                ConnectionTree.Expand(connectionInfo.Parent);
                connectionInfo = connectionInfo.Parent;
            }
        }

        private void TvConnections_KeyDown(object? sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;

                    if (Settings.Default.OpenMultipleConnectionsWithEnter)
                    {
                        HandleEnterKeyMultiSelect();
                    }
                    else
                    {
                        if (SelectedNode == null)
                            return;
                        ShellRuntime.ConnectionInitiator.OpenConnection(SelectedNode);
                    }
                }
            }
            catch (Exception ex)
            {
                ShellRuntime.MessageCollector.AddExceptionStackTrace("tvConnections_KeyDown (UI.Window.ConnectionTreeWindow) failed", ex);
            }
        }

        /// <summary>
        /// Handles opening multiple selected connections when Enter is pressed.
        /// Opens explicitly selected connections, or if none are selected, opens direct children of selected folders.
        /// </summary>
        private void HandleEnterKeyMultiSelect()
        {
            var connectionsToOpen = GetExplicitConnectionsToOpen();

            if (connectionsToOpen.Count == 0)
            {
                connectionsToOpen.AddRange(GetFolderConnectionsToOpen());
            }

            foreach (var connection in connectionsToOpen)
            {
                ShellRuntime.ConnectionInitiator.OpenConnection(connection);
            }
        }

        /// <summary>
        /// Gets explicitly selected connections that are not already open.
        /// </summary>
        private List<ConnectionInfo> GetExplicitConnectionsToOpen()
        {
            return ConnectionTree.SelectedObjects
                .OfType<ConnectionInfo>()
                .Where(n => n.GetTreeNodeType() == TreeNodeType.Connection
                         || n.GetTreeNodeType() == TreeNodeType.PuttySession)
                .Where(n => n.OpenConnections.Count == 0)
                .ToList();
        }

        /// <summary>
        /// Gets direct child connections from selected folders that are not already open.
        /// </summary>
        private List<ConnectionInfo> GetFolderConnectionsToOpen()
        {
            var connectionsFromFolders = new List<ConnectionInfo>();
            var selectedFolders = ConnectionTree.SelectedObjects
                .OfType<ConnectionInfo>()
                .Where(n => n.GetTreeNodeType() == TreeNodeType.Container)
                .ToList();

            foreach (var folder in selectedFolders)
            {
                var directChildren = ConnectionSelectionHelper.GetDirectChildConnections(folder)
                    .Where(n => n.OpenConnections.Count == 0)
                    .ToList();
                connectionsFromFolders.AddRange(directChildren);
            }

            return connectionsFromFolders;
        }

        #endregion
    }
}
