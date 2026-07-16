using System;
using System.Collections.Specialized;
using System.ComponentModel;
using LoipvRemote.Container;
using LoipvRemote.Connection;
using LoipvRemote.Properties;
using System.Runtime.Versioning;

namespace LoipvRemote.Config.Connections
{
    [SupportedOSPlatform("windows")]
    public class SaveConnectionsOnEdit
    {
        private readonly IConnectionTreeWorkspace _workspace;
        private readonly Func<bool> _isClosing;

        public SaveConnectionsOnEdit(IConnectionTreeWorkspace workspace, Func<bool>? isClosing = null)
        {
            ArgumentNullException.ThrowIfNull(workspace);

            _workspace = workspace;
            _isClosing = isClosing ?? (() => false);
            workspace.ConnectionsLoaded += WorkspaceOnConnectionsLoaded;
        }

        private void WorkspaceOnConnectionsLoaded(object? sender, ConnectionsLoadedEventArgs connectionsLoadedEventArgs)
        {
            connectionsLoadedEventArgs.NewConnectionTreeModel.CollectionChanged += ConnectionTreeModelOnCollectionChanged;
            connectionsLoadedEventArgs.NewConnectionTreeModel.PropertyChanged += ConnectionTreeModelOnPropertyChanged;

            foreach (Tree.ConnectionTreeModel oldTree in connectionsLoadedEventArgs.PreviousConnectionTreeModel)
            {
                oldTree.CollectionChanged -= ConnectionTreeModelOnCollectionChanged;
                oldTree.PropertyChanged -= ConnectionTreeModelOnPropertyChanged;
            }
        }

        private void ConnectionTreeModelOnPropertyChanged(object? sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            SaveConnectionOnEdit(propertyChangedEventArgs.PropertyName ?? string.Empty);
        }

        private void ConnectionTreeModelOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            SaveConnectionOnEdit();
        }

        private void SaveConnectionOnEdit(string propertyName = "")
        {
            if (ShouldPersistImmediately(propertyName) ||
                Properties.OptionsBackupPage.Default.SaveConnectionsFrequency == (int)ConnectionsBackupFrequency.OnEdit)
            {
                if (_isClosing())
                    return;

                _workspace.RequestSave(propertyName);
            }
        }

        internal static bool ShouldPersistImmediately(string? propertyName) =>
            propertyName == nameof(ContainerInfo.IsExpanded);
    }
}
