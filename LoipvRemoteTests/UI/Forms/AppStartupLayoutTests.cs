using LoipvRemote.UI.Forms;
using NUnit.Framework;
using System.Windows.Forms;

namespace LoipvRemoteTests.UI.Forms
{
    public class AppStartupLayoutTests
    {
        [Test]
        public void KeepsConnectionPropertyLabelsReadableAtStandardDpi()
        {
            Assert.That(AppStartupLayout.SidebarWidthForDpi(96), Is.EqualTo(400));
        }

        [Test]
        public void ScalesSidebarWidthWithDpi()
        {
            Assert.That(AppStartupLayout.SidebarWidthForDpi(120), Is.EqualTo(500));
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
