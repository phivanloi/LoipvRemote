using WeifenLuo.WinFormsUI.Docking;

namespace LoipvRemote.UI.Tabs
{
    internal static class LeftSidebarDockingPolicy
    {
        internal static bool UsesHiddenCaption(DockState dockState)
        {
            return dockState is DockState.DockLeft or DockState.DockLeftAutoHide;
        }

        internal static bool HidesTopTabStripBorder(DockState dockState)
        {
            return dockState is DockState.DockLeft or DockState.DockLeftAutoHide;
        }

        internal static void ConfigurePersistentSidebar(DockContent content)
        {
            content.DockHandler.ShowHint = DockState.DockLeft;
            content.DockHandler.CloseButton = false;
            content.DockHandler.CloseButtonVisible = false;
        }
    }
}
