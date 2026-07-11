using System.Drawing;
using LoipvRemote.UI.Tabs;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Tabs
{
    [TestFixture]
    public class DockTabMetricsTests
    {
        [Test]
        public void ToolWindowTextHasFivePixelsOfPaddingOnEachSide()
        {
            Rectangle textBounds = DockTabMetrics.TextBounds(new Rectangle(10, 20, 100, 30));

            Assert.That(textBounds, Is.EqualTo(new Rectangle(15, 25, 90, 20)));
            Assert.That(DockTabMetrics.BoxedTextWidth(80), Is.EqualTo(90));
            Assert.That(DockTabMetrics.BoxedTextHeight(18), Is.EqualTo(28));
        }

        [Test]
        public void DocumentTabContentHasFivePixelsOfPaddingAndALargerCloseButton()
        {
            Rectangle tabBounds = new(10, 20, 160, 32);

            Assert.That(DocumentTabMetrics.ContentBounds(tabBounds), Is.EqualTo(new Rectangle(15, 25, 150, 22)));
            Assert.That(DocumentTabMetrics.CloseButtonBounds(tabBounds), Is.EqualTo(new Rectangle(143, 25, 22, 22)));
            Assert.That(DocumentTabMetrics.MinimumHeight(18, 16), Is.EqualTo(32));
        }
    }
}
