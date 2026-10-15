using LoipvRemote.Domain.Connections;

namespace LoipvRemote.Domain.Validation;

public static class ConnectionTreeDefinitionValidator
{
    public static void Validate(ConnectionTreeDefinition tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(tree.Folders);
        ArgumentNullException.ThrowIfNull(tree.Connections);

        ConnectionFolderDefinition[] folders = tree.Folders.ToArray();
        ConnectionDefinition[] connections = tree.Connections.ToArray();
        var folderIds = new HashSet<Guid>();
        foreach (ConnectionFolderDefinition folder in folders)
        {
            if (folder.Id == Guid.Empty)
                throw new ArgumentException("Folder ID is required.", nameof(tree));
            if (!folderIds.Add(folder.Id))
                throw new ArgumentException("Folder IDs must be unique.", nameof(tree));
            if (string.IsNullOrWhiteSpace(folder.Name))
                throw new ArgumentException("Folder name is required.", nameof(tree));
            if (folder.SortOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(tree), "Folder sort order cannot be negative.");
            folder.Options?.Validate();
        }

        var nodeIds = new HashSet<Guid>(folderIds);
        foreach (ConnectionDefinition connection in connections)
        {
            ConnectionDefinitionValidator.Validate(connection);
            if (!nodeIds.Add(connection.Id))
                throw new ArgumentException("Folder and connection IDs must be unique across the tree.", nameof(tree));
            if (connection.ParentFolderId is { } parentFolderId && !folderIds.Contains(parentFolderId))
                throw new ArgumentException("A connection parent must reference an existing folder.", nameof(tree));
        }

        foreach (ConnectionFolderDefinition folder in folders)
        {
            if (folder.ParentFolderId is { } parentFolderId && !folderIds.Contains(parentFolderId))
                throw new ArgumentException("A folder parent must reference an existing folder.", nameof(tree));

            var ancestors = new HashSet<Guid> { folder.Id };
            Guid? currentParentId = folder.ParentFolderId;
            while (currentParentId is { } parentId)
            {
                if (!ancestors.Add(parentId))
                    throw new ArgumentException("Connection folders cannot contain a cycle.", nameof(tree));

                currentParentId = folders.Single(folder => folder.Id == parentId).ParentFolderId;
            }
        }
    }
}
