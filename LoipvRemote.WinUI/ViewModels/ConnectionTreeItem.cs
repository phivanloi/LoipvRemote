using LoipvRemote.Domain.Connections;

namespace LoipvRemote.WinUI.ViewModels;

/// <summary>Presentation-only item for the WinUI TreeView.</summary>
public sealed record ConnectionTreeItem(
    Guid Id,
    string DisplayName,
    bool IsFolder,
    IReadOnlyList<ConnectionTreeItem> Children,
    bool IsConnected = false,
    ProtocolKind? Protocol = null);
