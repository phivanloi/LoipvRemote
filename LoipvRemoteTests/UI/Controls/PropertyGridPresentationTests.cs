using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using LoipvRemote.UI.Controls.ConnectionInfoPropertyGrid;
using LoipvRemote.UI.Controls.FilteredPropertyGrid;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Controls
{
    [TestFixture]
    public class PropertyGridPresentationTests
    {
        [TestCase(16, 24)]
        [TestCase(21, 29)]
        public void RowHeightExpandsToFitTheCurrentFont(int fontHeight, int expectedRowHeight)
        {
            Assert.That(PropertyGridLayoutMetrics.RowHeightForFontHeight(fontHeight), Is.EqualTo(expectedRowHeight));
        }

        [Test]
        public void PlainValueDescriptorDoesNotMarkAChangedValueForBoldRendering()
        {
            PropertyDescriptor descriptor = TypeDescriptor.GetProperties(typeof(ExampleSettings))[nameof(ExampleSettings.Value)]!;
            PlainValuePropertyDescriptor plainDescriptor = new(descriptor);

            Assert.That(plainDescriptor.ShouldSerializeValue(new ExampleSettings { Value = "changed" }), Is.False);
        }

        [Test]
        public void ConnectionConfigurationGridAppliesTheScaledRowHeightToItsGridView()
        {
            using ConnectionInfoPropertyGrid grid = new();
            grid.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 14f);
            _ = grid.Handle;
            grid.ApplyScaledRowHeight();

            Control gridView = grid.Controls.Cast<Control>()
                .Single(control => control.GetType().Name == "PropertyGridView");
            PropertyInfo rowHeightProperty = gridView.GetType().GetProperty("RowHeight",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

            Assert.That((int)rowHeightProperty.GetValue(gridView)!,
                Is.EqualTo(PropertyGridLayoutMetrics.RowHeightForFontHeight(grid.Font.Height)));
        }

        private sealed class ExampleSettings
        {
            [DefaultValue("default")]
            public string Value { get; set; } = "default";
        }
    }
}
