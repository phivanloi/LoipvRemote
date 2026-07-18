using LoipvRemote.Domain.Connections;

namespace LoipvRemote.WinUI.ViewModels;

/// <summary>Projects Domain definitions into a deterministic, control-free tree.</summary>
public static class ConnectionTreeProjection
{
    private static readonly Guid RootId = Guid.Empty;

    public static IReadOnlyList<ConnectionTreeItem> Create(
        ConnectionTreeDefinition tree,
        IReadOnlySet<Guid>? connectedConnectionIds = null)
    {
        ArgumentNullException.ThrowIfNull(tree);

        Dictionary<Guid, List<ConnectionFolderDefinition>> foldersByParent = tree.Folders
            .GroupBy(folder => folder.ParentFolderId ?? RootId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(folder => folder.SortOrder)
                    .ThenBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList());
        Dictionary<Guid, List<ConnectionDefinition>> connectionsByParent = tree.Connections
            .GroupBy(connection => connection.ParentFolderId ?? RootId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(connection => connection.SortOrder)
                    .ThenBy(connection => connection.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList());

        return CreateRootChildren(foldersByParent, connectionsByParent, connectedConnectionIds ?? new HashSet<Guid>());
    }

    private static List<ConnectionTreeItem> CreateRootChildren(
        Dictionary<Guid, List<ConnectionFolderDefinition>> foldersByParent,
        Dictionary<Guid, List<ConnectionDefinition>> connectionsByParent,
        IReadOnlySet<Guid> connectedConnectionIds)
    {
        List<ConnectionFolderDefinition> rootFolders = foldersByParent.TryGetValue(RootId, out List<ConnectionFolderDefinition>? folders)
            ? [.. folders]
            : [];
        List<ConnectionDefinition> rootConnections = connectionsByParent.TryGetValue(RootId, out List<ConnectionDefinition>? connections)
            ? [.. connections]
            : [];

        foreach (ConnectionFolderDefinition rootFolder in rootFolders.Where(folder => folder.IsRoot).ToArray())
        {
            rootFolders.Remove(rootFolder);
            if (foldersByParent.TryGetValue(rootFolder.Id, out List<ConnectionFolderDefinition>? childFolders))
                rootFolders.AddRange(childFolders);
            if (connectionsByParent.TryGetValue(rootFolder.Id, out List<ConnectionDefinition>? childConnections))
                rootConnections.AddRange(childConnections);
        }

        return CreateItems(rootFolders, rootConnections, foldersByParent, connectionsByParent, connectedConnectionIds);
    }

    private static List<ConnectionTreeItem> CreateChildren(
        Guid parentFolderId,
        Dictionary<Guid, List<ConnectionFolderDefinition>> foldersByParent,
        Dictionary<Guid, List<ConnectionDefinition>> connectionsByParent,
        IReadOnlySet<Guid> connectedConnectionIds)
    {
        IReadOnlyList<ConnectionFolderDefinition> folders = foldersByParent.TryGetValue(parentFolderId, out List<ConnectionFolderDefinition>? folderItems)
            ? folderItems
            : [];
        IReadOnlyList<ConnectionDefinition> connections = connectionsByParent.TryGetValue(parentFolderId, out List<ConnectionDefinition>? connectionItems)
            ? connectionItems
            : [];

        return CreateItems(folders, connections, foldersByParent, connectionsByParent, connectedConnectionIds);
    }

    private static List<ConnectionTreeItem> CreateItems(
        IEnumerable<ConnectionFolderDefinition> folders,
        IEnumerable<ConnectionDefinition> connections,
        Dictionary<Guid, List<ConnectionFolderDefinition>> foldersByParent,
        Dictionary<Guid, List<ConnectionDefinition>> connectionsByParent,
        IReadOnlySet<Guid> connectedConnectionIds)
    {
        List<ConnectionTreeItem> children = [];
        foreach (ConnectionFolderDefinition folder in folders
                     .OrderBy(folder => folder.SortOrder)
                     .ThenBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            children.Add(new ConnectionTreeItem(
                folder.Id,
                folder.Name,
                true,
                CreateChildren(folder.Id, foldersByParent, connectionsByParent, connectedConnectionIds)));
        }

        children.AddRange(connections
            .OrderBy(connection => connection.SortOrder)
            .ThenBy(connection => connection.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(connection => new ConnectionTreeItem(
                connection.Id,
                $"{connection.Name}: {connection.Host}",
                false,
                [],
                connectedConnectionIds.Contains(connection.Id),
                connection.Protocol)));

        return children;
    }
}
