using System;
using LoipvRemote.UI.DesignSystem;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.DesignSystem
{
    [TestFixture]
    public class UiMetricsTests
    {
        [Test]
        public void PreferencesClampUnsafeValues()
        {
            UiPreferences preferences = new("", 500, 4, UiDensity.Standard);

            Assert.Multiple(() =>
            {
                Assert.That(preferences.FontFamily, Is.EqualTo("System"));
                Assert.That(preferences.FontScalePercent, Is.EqualTo(150));
                Assert.That(preferences.IconSize, Is.EqualTo(16));
            });
        }

        [TestCase(90, 9.45f)]
        [TestCase(100, 10.5f)]
        [TestCase(125, 13.125f)]
        [TestCase(150, 15.75f)]
        public void BodyFontUsesConfiguredScale(int percent, float expectedPoints)
        {
            UiMetrics metrics = new(new UiPreferences("System", percent, 20, UiDensity.Standard));
            Assert.That(metrics.BodyFontPoints, Is.EqualTo(expectedPoints).Within(0.001f));
        }

        [TestCase(UiDensity.Compact, 28, 32)]
        [TestCase(UiDensity.Standard, 32, 36)]
        [TestCase(UiDensity.Comfortable, 36, 40)]
        public void DensityControlsRowsAndInteractiveTargets(UiDensity density, int rowHeight, int targetHeight)
        {
            UiMetrics metrics = new(new UiPreferences("System", 100, 20, density));
            Assert.Multiple(() =>
            {
                Assert.That(metrics.RowHeight, Is.EqualTo(rowHeight));
                Assert.That(metrics.InteractiveHeight, Is.EqualTo(targetHeight));
                Assert.That(metrics.IconHitTarget, Is.GreaterThanOrEqualTo(32));
            });
        }

        [Test]
        public void DpiScalingRejectsInvalidScale()
        {
            UiMetrics metrics = new(new UiPreferences("System", 100, 20, UiDensity.Standard));
            Assert.Throws<ArgumentOutOfRangeException>(() => metrics.ScaleForDpi(20, 0));
        }

        [TestCase(1f, 20)]
        [TestCase(1.25f, 25)]
        [TestCase(1.5f, 30)]
        [TestCase(2f, 40)]
        public void DpiScalingReturnsPhysicalPixels(float dpiScale, int expected)
        {
            UiMetrics metrics = new(new UiPreferences("System", 100, 20, UiDensity.Standard));
            Assert.That(metrics.ScaleForDpi(20, dpiScale), Is.EqualTo(expected));
        }

        [TestCase(14, 26)]
        [TestCase(18, 28)]
        public void InputHeightKeepsTextVerticallyBalanced(int textHeight, int expectedHeight)
        {
            Assert.That(InputControlMetrics.InputHeight(textHeight), Is.EqualTo(expectedHeight));
        }

        [TestCase(10, 14)]
        [TestCase(17, 17)]
        [TestCase(24, 20)]
        public void CheckBoxGlyphSizeTracksTypography(int textHeight, int expectedGlyphSize)
        {
            Assert.That(InputControlMetrics.CheckBoxGlyphSize(textHeight), Is.EqualTo(expectedGlyphSize));
        }

        [TestCase(14, 22)]
        [TestCase(18, 26)]
        public void ComboBoxItemHeightTracksTypography(int textHeight, int expectedHeight)
        {
            Assert.That(InputControlMetrics.ComboBoxItemHeight(textHeight), Is.EqualTo(expectedHeight));
        }
    }
}
