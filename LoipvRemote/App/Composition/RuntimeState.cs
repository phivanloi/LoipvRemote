using LoipvRemote.Tools;
using LoipvRemote.UI;

namespace LoipvRemote.App.Composition;

/// <summary>Mutable UI state retained while Runtime callers are migrated to explicit dependencies.</summary>
public sealed class RuntimeState
{
    public WindowList? WindowList { get; set; }
    public NotificationAreaIcon? NotificationAreaIcon { get; set; }
}
