using LoipvRemote.UI.Forms;
using NUnit.Framework;
using System.Windows.Forms;

namespace LoipvRemoteTests.UI.Forms
{
    public class AppStartupLayoutTests
    {
        [Test]
        public void ConvertsDefaultSidebarWidthToDockPanelPortion()
        {
            Assert.That(AppStartupLayout.SidebarPortionForWidth(1920), Is.EqualTo(340d / 1920d).Within(0.0001));
            Assert.That(AppStartupLayout.SidebarPortionForWidth(2560, 120), Is.EqualTo(425d / 2560d).Within(0.0001));
        }

        [Test]
        public void ClampsSidebarPortionForNarrowAndWideDockSurfaces()
        {
            Assert.Multiple(() =>
            {
                Assert.That(AppStartupLayout.SidebarPortionForWidth(400), Is.EqualTo(0.5));
                Assert.That(AppStartupLayout.SidebarPortionForWidth(6400), Is.EqualTo(0.1));
            });
        }


        [TestCase(false, false, FormWindowState.Maximized)]
        [TestCase(true, false, FormWindowState.Minimized)]
        [TestCase(false, true, FormWindowState.Normal)]
        public void DefaultsToMaximizedUnlessAnExplicitStartupModeOverridesIt(bool startMinimized,
            bool startFullScreen, FormWindowState expected)
        {
            Assert.That(AppStartupLayout.ResolveWindowState(startMinimized, startFullScreen), Is.EqualTo(expected));
        }
    }
}
