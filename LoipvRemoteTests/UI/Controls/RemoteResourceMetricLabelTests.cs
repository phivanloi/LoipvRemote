using System.Drawing;
using LoipvRemote.UI.Controls;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Controls
{
    [TestFixture]
    public sealed class RemoteResourceMetricLabelTests
    {
        [Test]
        public void KeepsTheCaptionRegularAndRendersTheValueBold()
        {
            using ResourceMetricLabel label = new()
            {
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10f, FontStyle.Regular)
            };
            label.SetMetric("CPU:", "42 %");

            Assert.Multiple(() =>
            {
                Assert.That(label.Caption, Is.EqualTo("CPU:"));
                Assert.That(label.ValueText, Is.EqualTo("42 %"));
                Assert.That(label.ValueFontStyle.HasFlag(FontStyle.Bold), Is.True);
                Assert.That(label.Font.Style.HasFlag(FontStyle.Bold), Is.False);
            });
        }
    }
}
