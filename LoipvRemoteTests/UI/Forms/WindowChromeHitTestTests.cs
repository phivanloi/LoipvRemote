using System.Drawing;
using LoipvRemote.UI.Forms;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Forms
{
    [TestFixture]
    public class WindowChromeHitTestTests
    {
        [TestCase(0, 0, WindowChromeHitTest.TopLeft)]
        [TestCase(799, 0, WindowChromeHitTest.TopRight)]
        [TestCase(0, 599, WindowChromeHitTest.BottomLeft)]
        [TestCase(799, 599, WindowChromeHitTest.BottomRight)]
        [TestCase(400, 0, WindowChromeHitTest.Top)]
        [TestCase(400, 599, WindowChromeHitTest.Bottom)]
        [TestCase(0, 300, WindowChromeHitTest.Left)]
        [TestCase(799, 300, WindowChromeHitTest.Right)]
        [TestCase(400, 300, WindowChromeHitTest.Client)]
        public void ResolvesResizeEdgesAndCorners(int x, int y, int expected)
        {
            int hitTest = WindowChromeHitTest.ResolveResizeHitTest(new Size(800, 600), new Point(x, y), 8);

            Assert.That(hitTest, Is.EqualTo(expected));
        }

        [Test]
        public void DoesNotCreateResizeTargetWhenWindowIsTooSmall()
        {
            int hitTest = WindowChromeHitTest.ResolveResizeHitTest(new Size(10, 10), new Point(5, 5), 8);

            Assert.That(hitTest, Is.EqualTo(WindowChromeHitTest.Client));
        }

        [Test]
        public void CaptionButtonsFollowStandardWindowsOrder()
        {
            WindowCaptionButtonKind[] expected =
            [
                WindowCaptionButtonKind.Minimize,
                WindowCaptionButtonKind.Maximize,
                WindowCaptionButtonKind.Close
            ];

            Assert.That(WindowCaptionButtonOrder.Standard, Is.EqualTo(expected));
        }

        [Test]
        public void RecognizesCaptionDoubleClick()
        {
            Assert.That(WindowChromeHitTest.IsCaptionDoubleClick(WindowChromeHitTest.Caption), Is.True);
            Assert.That(WindowChromeHitTest.IsCaptionDoubleClick(WindowChromeHitTest.Client), Is.False);
        }
    }
}
