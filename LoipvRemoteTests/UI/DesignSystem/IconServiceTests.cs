using System;
using System.Drawing;
using LoipvRemote.UI.DesignSystem;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.DesignSystem
{
    [TestFixture]
    [Platform("Win")]
    public class IconServiceTests
    {
        [Test]
        public void ResizeReturnsRequestedSquareCanvas()
        {
            using Bitmap source = new(16, 16);
            source.SetPixel(7, 7, Color.Black);
            using Bitmap result = IconService.Resize(source, 24);

            Assert.That(result.Size, Is.EqualTo(new Size(24, 24)));
        }

        [Test]
        public void ResizeRejectsInvalidArguments()
        {
            using Bitmap source = new(16, 16);
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() => IconService.Resize(null!, 20));
                Assert.Throws<ArgumentOutOfRangeException>(() => IconService.Resize(source, 0));
            });
        }
    }
}
