using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.Versioning;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Properties;
using LoipvRemote.Tree.Root;


namespace LoipvRemote.Tree
{
    [SupportedOSPlatform("windows")]
    public sealed class ConnectionTreeModel : INotifyCollectionChanged, INotifyPropertyChanged
    {
        public List<ContainerInfo> RootNodes { get; } = [];

        public void AddRootNode(ContainerInfo rootNode)
        {
            if (RootNodes.Contains(rootNode)) return;
            RootNodes.Add(rootNode);
            rootNode.CollectionChanged += RaiseCollectionChangedEvent!;
            rootNode.PropertyChanged += RaisePropertyChangedEvent!;
            RaiseCollectionChangedEvent(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, rootNode));
        }

        public void RemoveRootNode(ContainerInfo rootNode)
        {
            if (!RootNodes.Contains(rootNode)) return;
            rootNode.CollectionChanged -= RaiseCollectionChangedEvent!;
            rootNode.PropertyChanged -= RaisePropertyChangedEvent!;
            RootNodes.Remove(rootNode);
            RaiseCollectionChangedEvent(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, rootNode));
        }

        public IReadOnlyList<ConnectionInfo> GetRecursiveChildList()
        {
            List<ConnectionInfo> list = new();
            foreach (ContainerInfo rootNode in RootNodes)
            {
                list.AddRange(GetRecursiveChildList(rootNode));
            }

            return list;
        }

        public static IEnumerable<ConnectionInfo> GetRecursiveChildList(ContainerInfo container)
        {
            return container.GetRecursiveChildList();
        }

        public static IEnumerable<ConnectionInfo> GetRecursiveFavoriteChildList(ContainerInfo container)
        {
            return container.GetRecursiveFavoriteChildList();
        }

        public static void RenameNode(ConnectionInfo connectionInfo, string newName)
        {
            if (newName == null || newName.Length <= 0)
                return;

            connectionInfo.Name = newName;
            if (Settings.Default.SetHostnameLikeDisplayName)
                connectionInfo.Hostname = newName;
        }

        public static void DeleteNode(ConnectionInfo connectionInfo)
        {
            if (connectionInfo is RootNodeInfo)
                return;

            connectionInfo?.RemoveParent();
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaiseCollectionChangedEvent(object? sender, NotifyCollectionChangedEventArgs args)
        {
            CollectionChanged?.Invoke(sender, args);
        }

        private void RaisePropertyChangedEvent(object? sender, PropertyChangedEventArgs args)
        {
            PropertyChanged?.Invoke(sender, args);
        }
    }
}