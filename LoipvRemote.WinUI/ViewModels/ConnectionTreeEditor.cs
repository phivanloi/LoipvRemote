using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;

namespace LoipvRemote.WinUI.ViewModels;

/// <summary>Pure tree mutations used by WinUI editors before persisting a definition store.</summary>
public static class ConnectionTreeEditor
{
    public static ConnectionTreeDefinition MergeImportedTree(
        ConnectionTreeDefinition destination,
        ConnectionTreeDefinition imported,
        string importFolderName)
        => MergeImportedTreeWithIdMap(destination, imported, importFolderName).Tree;

    /// <summary>
    /// Merges an imported tree and returns the generated connection IDs so a
    /// caller can re-protect portable credentials for their new identities.
    /// </summary>
    public static ConnectionTreeImportResult MergeImportedTreeWithIdMap(
        ConnectionTreeDefinition destination,
        ConnectionTreeDefinition imported,
        string importFolderName)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(imported);
        ArgumentException.ThrowIfNullOrWhiteSpace(importFolderName);
        destination.Validate();
        imported.Validate();

        var importRoot = new ConnectionFolderDefinition(
            Guid.NewGuid(),
            importFolderName.Trim(),
            SortOrder: NextSortOrder(destination.Folders));
        Dictionary<Guid, Guid> folderIds = imported.Folders.ToDictionary(folder => folder.Id, _ => Guid.NewGuid());
        Dictionary<Guid, Guid> connectionIds = imported.Connections.ToDictionary(connection => connection.Id, _ => Guid.NewGuid());

        ConnectionFolderDefinition[] importedFolders = imported.Folders
            .Select(folder => folder with
            {
                Id = folderIds[folder.Id],
                ParentFolderId = folder.ParentFolderId is { } parent && folderIds.TryGetValue(parent, out Guid mappedParent)
                    ? mappedParent
                    : importRoot.Id,
                IsRoot = false
            })
            .ToArray();
        ConnectionDefinition[] importedConnections = imported.Connections
            .Select(connection => connection with
            {
                Id = connectionIds[connection.Id],
                ParentFolderId = connection.ParentFolderId is { } parent && folderIds.TryGetValue(parent, out Guid mappedParent)
                    ? mappedParent
                    : importRoot.Id
            })
            .ToArray();
        var updated = destination with
        {
            Folders = destination.Folders.Append(importRoot).Concat(importedFolders).ToArray(),
            Connections = destination.Connections.Concat(importedConnections).ToArray()
        };
        updated.Validate();
        return new ConnectionTreeImportResult(updated, connectionIds);
    }

    public static ConnectionTreeDefinition AddRootFolder(ConnectionTreeDefinition tree, string name)
        => AddFolder(tree, name, parentFolderId: null);

    public static ConnectionTreeDefinition AddFolder(
        ConnectionTreeDefinition tree,
        string name,
        Guid? parentFolderId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateParentFolder(tree, parentFolderId);

        int sortOrder = NextFolderSortOrder(tree, parentFolderId);
        var folder = new ConnectionFolderDefinition(Guid.NewGuid(), name.Trim(), parentFolderId, sortOrder);
        var updated = tree with { Folders = tree.Folders.Append(folder).ToArray() };
        updated.Validate();
        return updated;
    }

    public static ConnectionTreeDefinition AddRootConnection(
        ConnectionTreeDefinition tree,
        string name,
        string host,
        int port,
        ProtocolKind protocol,
        ConnectionNodeOptions? options = null,
        Guid? connectionId = null)
        => AddConnection(tree, name, host, port, protocol, parentFolderId: null, options, connectionId);

    public static ConnectionTreeDefinition AddConnection(
        ConnectionTreeDefinition tree,
        string name,
        string host,
        int port,
        ProtocolKind protocol,
        Guid? parentFolderId,
        ConnectionNodeOptions? options = null,
        Guid? connectionId = null,
        CredentialReference? credential = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ValidateParentFolder(tree, parentFolderId);

        int sortOrder = NextConnectionSortOrder(tree, parentFolderId);
        var connection = new ConnectionDefinition(
            connectionId ?? Guid.NewGuid(),
            name.Trim(),
            host.Trim(),
            port,
            protocol,
            credential ?? CredentialReference.None,
            ParentFolderId: parentFolderId,
            SortOrder: sortOrder,
            Options: options);
        var updated = tree with { Connections = tree.Connections.Append(connection).ToArray() };
        updated.Validate();
        return updated;
    }

    public static ConnectionTreeDefinition UpdateConnection(
        ConnectionTreeDefinition tree,
        Guid connectionId,
        string name,
        string host,
        int port)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ConnectionDefinition original = tree.Connections.SingleOrDefault(connection => connection.Id == connectionId)
            ?? throw new ArgumentException("The connection no longer exists in the current tree.", nameof(connectionId));
        return UpdateConnection(tree, connectionId, name, host, port, original.Options);
    }

    public static ConnectionTreeDefinition UpdateConnection(
        ConnectionTreeDefinition tree,
        Guid connectionId,
        string name,
        string host,
        int port,
        ConnectionNodeOptions? options,
        CredentialReference? credential = null,
        ProtocolKind? protocol = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        ConnectionDefinition original = tree.Connections.SingleOrDefault(connection => connection.Id == connectionId)
            ?? throw new ArgumentException("The connection no longer exists in the current tree.", nameof(connectionId));
        ConnectionDefinition edited = original with
        {
            Name = name.Trim(),
            Host = host.Trim(),
            Port = port,
            Options = options,
            Credential = credential ?? original.Credential,
            Protocol = protocol ?? original.Protocol
        };
        var updated = tree with
        {
            Connections = tree.Connections.Select(connection => connection.Id == connectionId ? edited : connection).ToArray()
        };
        updated.Validate();
        return updated;
    }

    public static ConnectionTreeDefinition RenameFolder(
        ConnectionTreeDefinition tree,
        Guid folderId,
        string name)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ConnectionFolderDefinition original = tree.Folders.SingleOrDefault(folder => folder.Id == folderId)
            ?? throw new ArgumentException("The folder no longer exists in the current tree.", nameof(folderId));
        return UpdateFolder(tree, folderId, name, original.Options);
    }

    public static ConnectionTreeDefinition UpdateFolder(
        ConnectionTreeDefinition tree,
        Guid folderId,
        string name,
        ConnectionNodeOptions? options)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ConnectionFolderDefinition original = tree.Folders.SingleOrDefault(folder => folder.Id == folderId)
            ?? throw new ArgumentException("The folder no longer exists in the current tree.", nameof(folderId));

        var updated = tree with
        {
            Folders = tree.Folders.Select(folder => folder.Id == folderId
                ? original with { Name = name.Trim(), Options = options }
                : folder).ToArray()
        };
        updated.Validate();
        return updated;
    }

    public static ConnectionTreeDefinition MoveConnection(
        ConnectionTreeDefinition tree,
        Guid connectionId,
        Guid? destinationFolderId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ValidateParentFolder(tree, destinationFolderId);
        ConnectionDefinition connection = tree.Connections.SingleOrDefault(candidate => candidate.Id == connectionId)
            ?? throw new ArgumentException("The connection no longer exists in the current tree.", nameof(connectionId));
        int sortOrder = NextConnectionSortOrder(tree, destinationFolderId, connectionId);
        var updated = tree with
        {
            Connections = tree.Connections.Select(candidate => candidate.Id == connectionId
                ? connection with { ParentFolderId = destinationFolderId, SortOrder = sortOrder }
                : candidate).ToArray()
        };
        updated.Validate();
        return updated;
    }

    public static ConnectionTreeDefinition MoveFolder(
        ConnectionTreeDefinition tree,
        Guid folderId,
        Guid? destinationParentFolderId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ConnectionFolderDefinition folder = tree.Folders.SingleOrDefault(candidate => candidate.Id == folderId)
            ?? throw new ArgumentException("The folder no longer exists in the current tree.", nameof(folderId));
        ValidateParentFolder(tree, destinationParentFolderId);
        if (destinationParentFolderId == folderId || IsDescendant(tree, destinationParentFolderId, folderId))
            throw new ArgumentException("A folder cannot be moved into itself or one of its descendants.", nameof(destinationParentFolderId));

        int sortOrder = NextFolderSortOrder(tree, destinationParentFolderId, folderId);
        var updated = tree with
        {
            Folders = tree.Folders.Select(candidate => candidate.Id == folderId
                ? folder with { ParentFolderId = destinationParentFolderId, SortOrder = sortOrder, IsRoot = destinationParentFolderId is null && folder.IsRoot }
                : candidate).ToArray()
        };
        updated.Validate();
        return updated;
    }

    public static ConnectionTreeDefinition DuplicateConnection(ConnectionTreeDefinition tree, Guid connectionId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ConnectionDefinition source = tree.Connections.SingleOrDefault(connection => connection.Id == connectionId)
            ?? throw new ArgumentException("The connection no longer exists in the current tree.", nameof(connectionId));
        ConnectionDefinition copy = source with
        {
            Id = Guid.NewGuid(),
            Name = $"{source.Name} (Copy)",
            SortOrder = NextConnectionSortOrder(tree, source.ParentFolderId)
        };
        var updated = tree with { Connections = tree.Connections.Append(copy).ToArray() };
        updated.Validate();
        return updated;
    }

    public static ConnectionTreeDefinition DuplicateFolder(ConnectionTreeDefinition tree, Guid folderId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ConnectionFolderDefinition source = tree.Folders.SingleOrDefault(folder => folder.Id == folderId)
            ?? throw new ArgumentException("The folder no longer exists in the current tree.", nameof(folderId));

        var copiedFolderIds = new Dictionary<Guid, Guid>();
        var copiedFolders = new List<ConnectionFolderDefinition>();
        CopyFolder(source, source.ParentFolderId, isTopLevelCopy: true);

        ConnectionDefinition[] copiedConnections = tree.Connections
            .Where(connection => connection.ParentFolderId is { } parent && copiedFolderIds.ContainsKey(parent))
            .Select(connection => connection with
            {
                Id = Guid.NewGuid(),
                ParentFolderId = copiedFolderIds[connection.ParentFolderId!.Value]
            })
            .ToArray();
        var updated = tree with
        {
            Folders = tree.Folders.Concat(copiedFolders).ToArray(),
            Connections = tree.Connections.Concat(copiedConnections).ToArray()
        };
        updated.Validate();
        return updated;

        void CopyFolder(ConnectionFolderDefinition current, Guid? copiedParentId, bool isTopLevelCopy)
        {
            Guid copiedId = Guid.NewGuid();
            copiedFolderIds.Add(current.Id, copiedId);
            copiedFolders.Add(current with
            {
                Id = copiedId,
                Name = isTopLevelCopy ? $"{current.Name} (Copy)" : current.Name,
                ParentFolderId = copiedParentId,
                SortOrder = isTopLevelCopy ? NextFolderSortOrder(tree, copiedParentId) : current.SortOrder,
                IsRoot = isTopLevelCopy && current.IsRoot
            });

            foreach (ConnectionFolderDefinition child in tree.Folders
                         .Where(folder => folder.ParentFolderId == current.Id)
                         .OrderBy(folder => folder.SortOrder)
                         .ThenBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                CopyFolder(child, copiedId, isTopLevelCopy: false);
            }
        }
    }

    public static ConnectionTreeDefinition DeleteConnection(ConnectionTreeDefinition tree, Guid connectionId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (!tree.Connections.Any(connection => connection.Id == connectionId))
            throw new ArgumentException("The connection no longer exists in the current tree.", nameof(connectionId));
        var updated = tree with { Connections = tree.Connections.Where(connection => connection.Id != connectionId).ToArray() };
        updated.Validate();
        return updated;
    }

    public static ConnectionTreeDefinition DeleteFolder(ConnectionTreeDefinition tree, Guid folderId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (!tree.Folders.Any(folder => folder.Id == folderId))
            throw new ArgumentException("The folder no longer exists in the current tree.", nameof(folderId));

        var removedFolderIds = new HashSet<Guid> { folderId };
        bool changed;
        do
        {
            changed = false;
            foreach (ConnectionFolderDefinition folder in tree.Folders.Where(folder => folder.ParentFolderId is { } parent && removedFolderIds.Contains(parent)))
                changed |= removedFolderIds.Add(folder.Id);
        } while (changed);

        var updated = tree with
        {
            Folders = tree.Folders.Where(folder => !removedFolderIds.Contains(folder.Id)).ToArray(),
            Connections = tree.Connections.Where(connection => connection.ParentFolderId is not { } parent || !removedFolderIds.Contains(parent)).ToArray()
        };
        updated.Validate();
        return updated;
    }

    public static ConnectionTreeDefinition ReorderConnection(ConnectionTreeDefinition tree, Guid connectionId, int offset)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (offset == 0)
            return tree;
        ConnectionDefinition selected = tree.Connections.SingleOrDefault(connection => connection.Id == connectionId)
            ?? throw new ArgumentException("The connection no longer exists in the current tree.", nameof(connectionId));
        List<ConnectionDefinition> siblings = tree.Connections
            .Where(connection => connection.ParentFolderId == selected.ParentFolderId)
            .OrderBy(connection => connection.SortOrder)
            .ThenBy(connection => connection.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        int index = siblings.FindIndex(connection => connection.Id == connectionId);
        int targetIndex = Math.Clamp(index + offset, 0, siblings.Count - 1);
        if (index == targetIndex)
            return tree;

        siblings.RemoveAt(index);
        siblings.Insert(targetIndex, selected);
        Dictionary<Guid, int> sortOrders = siblings.Select((connection, order) => (connection.Id, order)).ToDictionary(pair => pair.Id, pair => pair.order);
        var updated = tree with
        {
            Connections = tree.Connections.Select(connection => sortOrders.TryGetValue(connection.Id, out int order)
                ? connection with { SortOrder = order }
                : connection).ToArray()
        };
        updated.Validate();
        return updated;
    }

    public static ConnectionTreeDefinition ReorderFolder(ConnectionTreeDefinition tree, Guid folderId, int offset)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (offset == 0)
            return tree;
        ConnectionFolderDefinition selected = tree.Folders.SingleOrDefault(folder => folder.Id == folderId)
            ?? throw new ArgumentException("The folder no longer exists in the current tree.", nameof(folderId));
        List<ConnectionFolderDefinition> siblings = tree.Folders
            .Where(folder => folder.ParentFolderId == selected.ParentFolderId)
            .OrderBy(folder => folder.SortOrder)
            .ThenBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        int index = siblings.FindIndex(folder => folder.Id == folderId);
        int targetIndex = Math.Clamp(index + offset, 0, siblings.Count - 1);
        if (index == targetIndex)
            return tree;

        siblings.RemoveAt(index);
        siblings.Insert(targetIndex, selected);
        Dictionary<Guid, int> sortOrders = siblings.Select((folder, order) => (folder.Id, order)).ToDictionary(pair => pair.Id, pair => pair.order);
        var updated = tree with
        {
            Folders = tree.Folders.Select(folder => sortOrders.TryGetValue(folder.Id, out int order)
                ? folder with { SortOrder = order }
                : folder).ToArray()
        };
        updated.Validate();
        return updated;
    }

    private static void ValidateParentFolder(ConnectionTreeDefinition tree, Guid? parentFolderId)
    {
        if (parentFolderId is { } parent && !tree.Folders.Any(folder => folder.Id == parent))
            throw new ArgumentException("The destination folder no longer exists in the current tree.", nameof(parentFolderId));
    }

    private static bool IsDescendant(ConnectionTreeDefinition tree, Guid? candidateId, Guid ancestorId)
    {
        while (candidateId is { } currentId)
        {
            if (currentId == ancestorId)
                return true;
            candidateId = tree.Folders.Single(folder => folder.Id == currentId).ParentFolderId;
        }

        return false;
    }

    private static int NextFolderSortOrder(ConnectionTreeDefinition tree, Guid? parentFolderId, Guid? excludedFolderId = null) =>
        tree.Folders.Where(folder => folder.ParentFolderId == parentFolderId && folder.Id != excludedFolderId)
            .Select(folder => folder.SortOrder)
            .DefaultIfEmpty(-1)
            .Max() + 1;

    private static int NextConnectionSortOrder(ConnectionTreeDefinition tree, Guid? parentFolderId, Guid? excludedConnectionId = null) =>
        tree.Connections.Where(connection => connection.ParentFolderId == parentFolderId && connection.Id != excludedConnectionId)
            .Select(connection => connection.SortOrder)
            .DefaultIfEmpty(-1)
            .Max() + 1;

    private static int NextSortOrder<T>(IReadOnlyCollection<T> nodes) where T : class
    {
        return nodes.Count == 0
            ? 0
            : nodes switch
            {
                IReadOnlyCollection<ConnectionFolderDefinition> folders => folders.Max(folder => folder.SortOrder) + 1,
                IReadOnlyCollection<ConnectionDefinition> connections => connections.Max(connection => connection.SortOrder) + 1,
                _ => throw new ArgumentException("Unsupported connection tree node collection.", nameof(nodes))
            };
    }
}

/// <summary>Result of importing a tree whose identifiers were regenerated.</summary>
public sealed record ConnectionTreeImportResult(
    ConnectionTreeDefinition Tree,
    IReadOnlyDictionary<Guid, Guid> ConnectionIds);
