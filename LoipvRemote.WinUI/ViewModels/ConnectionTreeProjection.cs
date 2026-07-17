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

        return CreateChildren(RootId, foldersByParent, connectionsByParent, connectedConnectionIds ?? new HashSet<Guid>());
    }

    private static List<ConnectionTreeItem> CreateChildren(
        Guid parentFolderId,
        IReadOnlyDictionary<Guid, List<ConnectionFolderDefinition>> foldersByParent,
        IReadOnlyDictionary<Guid, List<ConnectionDefinition>> connectionsByParent,
        IReadOnlySet<Guid> connectedConnectionIds)
    {
        List<ConnectionTreeItem> children = [];

        if (foldersByParent.TryGetValue(parentFolderId, out List<ConnectionFolderDefinition>? folders))
        {
            foreach (ConnectionFolderDefinition folder in folders)
            {
                children.Add(new ConnectionTreeItem(
                    folder.Id,
                    folder.Name,
                    true,
                    CreateChildren(folder.Id, foldersByParent, connectionsByParent, connectedConnectionIds)));
            }
        }

        if (connectionsByParent.TryGetValue(parentFolderId, out List<ConnectionDefinition>? connections))
        {
            children.AddRange(connections.Select(connection => new ConnectionTreeItem(
                connection.Id,
                $"{connection.Name}: {connection.Host}",
                false,
                [],
                connectedConnectionIds.Contains(connection.Id),
                connection.Protocol)));
        }

        return children;
    }
}
