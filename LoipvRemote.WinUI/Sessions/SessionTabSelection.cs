namespace LoipvRemote.WinUI.Sessions;

/// <summary>Selects the nearest surviving session tab after the active tab closes.</summary>
internal static class SessionTabSelection
{
    public static T? SelectAfterClose<T>(IReadOnlyList<T> tabs, T closingTab)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(closingTab);

        int closingIndex = -1;
        for (int index = 0; index < tabs.Count; index++)
        {
            if (ReferenceEquals(tabs[index], closingTab))
            {
                closingIndex = index;
                break;
            }
        }

        if (closingIndex < 0)
            return null;

        // Prefer the tab that occupied the next slot. If the closing tab was
        // last, select its immediate predecessor instead.
        if (closingIndex + 1 < tabs.Count)
            return tabs[closingIndex + 1];
        return closingIndex > 0 ? tabs[closingIndex - 1] : null;
    }
}
