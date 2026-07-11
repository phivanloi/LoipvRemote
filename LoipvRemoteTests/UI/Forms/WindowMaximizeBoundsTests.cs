using System.Drawing;
using LoipvRemote.UI.Forms;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Forms
{
    public class WindowMaximizeBoundsTests
    {
        [Test]
        public void UsesWorkingAreaSoTheTaskbarRemainsVisible()
        {
            Rectangle monitor = new(0, 0, 1920, 1080);
            Rectangle workingArea = new(0, 0, 1920, 1040);

            Assert.That(WindowMaximizeBounds.Resolve(monitor, workingArea), Is.EqualTo(workingArea));
        }

        [Test]
        public void FallsBackToMonitorBoundsWhenWorkingAreaIsUnavailable()
        {
            Rectangle monitor = new(0, 0, 1920, 1080);

            Assert.That(WindowMaximizeBounds.Resolve(monitor, Rectangle.Empty), Is.EqualTo(monitor));
        }
    }
}
