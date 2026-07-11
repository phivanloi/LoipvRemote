using LoipvRemote.App;
using LoipvRemote.UI.Forms;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Forms
{
    [TestFixture]
    public class WindowTaskbarStyleTests
    {
        [Test]
        public void BorderlessWindowKeepsItsStyleAndSupportsStandardTaskbarCommands()
        {
            int borderlessStyle = NativeMethods.WS_POPUP | NativeMethods.WS_VISIBLE;

            int style = WindowTaskbarStyle.AddStandardTaskbarCommands(borderlessStyle);

            Assert.That(style & NativeMethods.WS_POPUP, Is.EqualTo(NativeMethods.WS_POPUP));
            Assert.That(style & NativeMethods.WS_VISIBLE, Is.EqualTo(NativeMethods.WS_VISIBLE));
            Assert.That(style & NativeMethods.WS_SYSMENU, Is.EqualTo(NativeMethods.WS_SYSMENU));
            Assert.That(style & NativeMethods.WS_MINIMIZEBOX, Is.EqualTo(NativeMethods.WS_MINIMIZEBOX));
            Assert.That(style & NativeMethods.WS_MAXIMIZEBOX, Is.EqualTo(NativeMethods.WS_MAXIMIZEBOX));
        }
    }
}
