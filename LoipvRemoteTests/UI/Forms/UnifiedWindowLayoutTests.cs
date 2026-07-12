using System.Drawing;
using LoipvRemote.UI.Forms;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Forms
{
    [TestFixture]
    public class UnifiedWindowLayoutTests
    {
        [TestCase(44, 35)]
        [TestCase(58, 46)]
        public void TopMenuHeightIsReducedByTwentyPercent(int originalHeight, int expectedHeight)
        {
            Assert.That(UnifiedWindowHeader.ReduceHeaderHeight(originalHeight), Is.EqualTo(expectedHeight));
        }

        [TestCase(35, 400)]
        [TestCase(58, 464)]
        public void MenuHostKeepsAllMainMenuItemsVisible(int headerHeight, int expectedWidth)
        {
            Assert.That(UnifiedWindowHeader.MenuHostWidthFor(headerHeight), Is.EqualTo(expectedWidth));
        }

        [Test]
        public void ContentBeginsBelowVisibleHeader()
        {
            Rectangle bounds = UnifiedWindowLayout.ContentBounds(new Size(1200, 800), 48, headerVisible: true);

            Assert.That(bounds, Is.EqualTo(new Rectangle(0, 48, 1200, 752)));
        }

        [Test]
        public void ContentFillsClientAreaWhenHeaderIsHidden()
        {
            Rectangle bounds = UnifiedWindowLayout.ContentBounds(new Size(1200, 800), 48, headerVisible: false);

            Assert.That(bounds, Is.EqualTo(new Rectangle(0, 0, 1200, 800)));
        }
    }
}
