namespace LoipvRemote.UseCases.Navigation;

/// <summary>Pure navigation rules for a desktop connection-tab collection.</summary>
public static class ConnectionTabNavigator
{
    public static bool TryGetRelativeIndex(int tabCount, int activeIndex, int offset, out int targetIndex)
    {
        targetIndex = -1;
        if (tabCount <= 1 || activeIndex < 0 || activeIndex >= tabCount || offset == 0)
            return false;

        targetIndex = ((activeIndex + offset) % tabCount + tabCount) % tabCount;
        return true;
    }

    public static bool IsValidIndex(int tabCount, int index) => index >= 0 && index < tabCount;
}
