namespace LoipvRemote.Domain.Connections;

/// <summary>Secret-free folder node used to organize connection definitions.</summary>
public sealed record ConnectionFolderDefinition(
    Guid Id,
    string Name,
    Guid? ParentFolderId = null,
    int SortOrder = 0,
    ConnectionNodeOptions? Options = null,
    bool IsRoot = false);
