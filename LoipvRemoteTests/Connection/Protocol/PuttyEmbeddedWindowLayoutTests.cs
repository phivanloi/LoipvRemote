using System.Drawing;
using LoipvRemote.Infrastructure.Windows.Interop;
using LoipvRemote.Connection.Protocol;
using LoipvRemote.Protocols.Putty;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol
{
    [TestFixture]
    public class PuttyEmbeddedWindowLayoutTests
    {
        [Test]
        public void CreateBorderlessChildStyleRemovesEveryNonClientChromeFlag()
        {
            int originalStyle = NativeMethods.WS_CAPTION |
                                NativeMethods.WS_THICKFRAME |
                                NativeMethods.WS_SYSMENU |
                                NativeMethods.WS_MINIMIZEBOX |
                                NativeMethods.WS_MAXIMIZEBOX |
                                NativeMethods.WS_POPUP |
                                NativeMethods.WS_CHILD |
                                NativeMethods.WS_VISIBLE;

            int embeddedStyle = PuttyEmbeddedWindowLayout.CreateBorderlessChildStyle(originalStyle);

            Assert.That(embeddedStyle & NativeMethods.WS_CHILD, Is.EqualTo(NativeMethods.WS_CHILD));
            Assert.That(embeddedStyle & NativeMethods.WS_CAPTION, Is.Zero);
            Assert.That(embeddedStyle & NativeMethods.WS_THICKFRAME, Is.Zero);
            Assert.That(embeddedStyle & NativeMethods.WS_SYSMENU, Is.Zero);
            Assert.That(embeddedStyle & NativeMethods.WS_MINIMIZEBOX, Is.Zero);
            Assert.That(embeddedStyle & NativeMethods.WS_MAXIMIZEBOX, Is.Zero);
            Assert.That(embeddedStyle & NativeMethods.WS_POPUP, Is.Zero);
            Assert.That(embeddedStyle & NativeMethods.WS_CHILD, Is.EqualTo(NativeMethods.WS_CHILD));
        }

        [Test]
        public void ContentBoundsUseTheHostClientAreaWithoutCaptionOffsets()
        {
            Rectangle clientArea = new Rectangle(0, 0, 1280, 720);

            Assert.That(PuttyEmbeddedWindowLayout.ContentBounds(clientArea), Is.EqualTo(clientArea));
        }

        [Test]
        public void ContentBoundsCropThePuttyClientTitleStrip()
        {
            Rectangle clientArea = new Rectangle(0, 0, 1280, 720);

            Rectangle contentBounds = PuttyEmbeddedWindowLayout.ContentBounds(clientArea, 31);

            Assert.That(contentBounds, Is.EqualTo(new Rectangle(0, -31, 1280, 751)));
        }
    }
}
