using System.ComponentModel;
using WeifenLuo.WinFormsUI.Docking;

namespace LoipvRemote.UI.Tabs
{
    /// <summary>
    /// Removes the caption bar from the persistent left sidebar. Its tab strip
    /// remains available below the content for Connections and Config.
    /// </summary>
    [ToolboxItem(false)]
    internal sealed class HiddenDockPaneCaption : DockPaneCaptionBase
    {
        public HiddenDockPaneCaption(DockPane pane) : base(pane)
        {
        }

        protected override int MeasureHeight() => 0;

        protected override bool CanDragAutoHide => false;
    }
}
