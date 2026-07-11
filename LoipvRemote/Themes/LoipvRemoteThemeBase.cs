using System.Drawing;
using System.Runtime.Versioning;
using LoipvRemote.UI.Tabs;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace LoipvRemote.Themes
{
    [SupportedOSPlatform("windows")]

    /// <summary>
    /// Visual Studio 2015 Light theme.
    /// </summary>
    public class MremoteNGThemeBase : VS2015ThemeBase
    {
        public MremoteNGThemeBase(byte[] themeResource)
            : base(themeResource)
        {
            Measures.SplitterSize = 3;
            Measures.AutoHideSplitterSize = 3;
            Measures.DockPadding = 0;
            ShowAutoHideContentOnHover = false;
        }
    }

    [SupportedOSPlatform("windows")]
    public class MremoteDockPaneStripFactory : DockPanelExtender.IDockPaneStripFactory
    {
        public DockPaneStripBase CreateDockPaneStrip(DockPane pane) => new DockPaneStripNG(pane);
    }

    [SupportedOSPlatform("windows")]
    public class LargeDockPaneCaptionFactory : DockPanelExtender.IDockPaneCaptionFactory
    {
        public DockPaneCaptionBase CreateDockPaneCaption(DockPane pane) =>
            LeftSidebarDockingPolicy.UsesHiddenCaption(pane.DockState)
                ? new HiddenDockPaneCaption(pane)
                : new LargeDockPaneCaption(pane);
    }

    public class MremoteFloatWindowFactory : DockPanelExtender.IFloatWindowFactory
    {
        public FloatWindow CreateFloatWindow(DockPanel dockPanel, DockPane pane, Rectangle bounds)
        {
            Rectangle? activeDocumentBounds = (dockPanel?.ActiveDocument as ConnectionTab)?.Bounds;

            // dockPanel is non-null per the IFloatWindowFactory.CreateFloatWindow contract
            return new FloatWindowNG(dockPanel!, pane, activeDocumentBounds ?? bounds);
        }

        public FloatWindow CreateFloatWindow(DockPanel dockPanel, DockPane pane)
        {
            return new FloatWindowNG(dockPanel, pane);
        }
    }
}
