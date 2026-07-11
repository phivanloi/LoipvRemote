using LoipvRemote.UI.Tabs;
using NUnit.Framework;
using WeifenLuo.WinFormsUI.Docking;

namespace LoipvRemoteTests.UI.Tabs
{
    public class LeftSidebarDockingPolicyTests
    {
        [TestCase(DockState.DockLeft, true)]
        [TestCase(DockState.DockLeftAutoHide, true)]
        [TestCase(DockState.DockRight, false)]
        [TestCase(DockState.Document, false)]
        public void HidesOnlyLeftSidebarCaptions(DockState dockState, bool expected)
        {
            Assert.That(LeftSidebarDockingPolicy.UsesHiddenCaption(dockState), Is.EqualTo(expected));
        }

        [TestCase(DockState.DockLeft, true)]
        [TestCase(DockState.DockLeftAutoHide, true)]
        [TestCase(DockState.DockRight, false)]
        [TestCase(DockState.Document, false)]
        public void HidesOnlyTheLeftSidebarTopTabStripBorder(DockState dockState, bool expected)
        {
            Assert.That(LeftSidebarDockingPolicy.HidesTopTabStripBorder(dockState), Is.EqualTo(expected));
        }
    }
}
