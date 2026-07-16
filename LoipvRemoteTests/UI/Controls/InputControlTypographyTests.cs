using System.Threading;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LoipvRemote.UI.Controls;
using LoipvRemote.UI.DesignSystem;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Controls
{
    [TestFixture]
    public class InputControlTypographyTests
    {
        [Test]
        [Apartment(ApartmentState.STA)]
        public void StandardInputControlsStartWithTheBodyFont()
        {
            MrngTextBox textBox = new();
            MrngNumericUpDown numericUpDown = new();
            MrngComboBox comboBox = new();

            Assert.Multiple(() =>
            {
                Assert.That(textBox.Font.SizeInPoints, Is.EqualTo(10f));
                Assert.That(numericUpDown.Font.SizeInPoints, Is.EqualTo(10f));
                Assert.That(comboBox.Font.SizeInPoints, Is.EqualTo(10f));
            });
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void HoverRestoresTheConfiguredFontInsteadOfKeepingASmallerNativeFont()
        {
            TestableTextBox textBox = new();
            UiScaleManager.Instance.Apply(textBox);
            float expectedSize = textBox.Font.SizeInPoints;
            textBox.Font = new Font(textBox.Font.FontFamily, 7f, textBox.Font.Style);

            textBox.RaiseMouseEnter();

            Assert.That(textBox.Font.SizeInPoints, Is.EqualTo(expectedSize));
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void InputFontIsRestoredWhenWinFormsChangesItAfterFocus()
        {
            using MrngTextBox textBox = new();
            UiScaleManager.Instance.Apply(textBox);
            float expectedSize = textBox.Font.SizeInPoints;

            textBox.Font = new Font(textBox.Font.FontFamily, 7f, textBox.Font.Style);

            Assert.That(textBox.Font.SizeInPoints, Is.EqualTo(expectedSize));
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void DropdownFontIsRestoredWhenWinFormsChangesItAfterHover()
        {
            using MrngComboBox comboBox = new();
            UiScaleManager.Instance.Apply(comboBox);
            float expectedSize = comboBox.Font.SizeInPoints;

            comboBox.Font = new Font(comboBox.Font.FontFamily, 7f, comboBox.Font.Style);

            Assert.That(comboBox.Font.SizeInPoints, Is.EqualTo(expectedSize));
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void DropdownItemsUseTheSameTypographyAsTheClosedInput()
        {
            using MrngComboBox comboBox = new();
            UiScaleManager.Instance.Apply(comboBox);

            Assert.That(comboBox.ItemHeight,
                Is.EqualTo(InputControlMetrics.ComboBoxItemHeight(comboBox.Font.Height)));
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void InputAndLabelUseTheSameBodyTypographyAfterHover()
        {
            using MrngLabel label = new();
            using TestableComboBox comboBox = new();
            UiScaleManager.Instance.Apply(label);
            UiScaleManager.Instance.Apply(comboBox);

            comboBox.RaiseMouseEnter();

            Assert.That(comboBox.Font.SizeInPoints, Is.EqualTo(label.Font.SizeInPoints));
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void IpInputKeepsItsVisibleOctetsBoundToTheScaledControlFont()
        {
            using MrngIpTextBox ipInput = new() { Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12f) };
            UiScaleManager.Instance.Apply(ipInput);

            Assert.Multiple(() =>
            {
                Assert.That(ipInput.Controls[0].Controls.Contains(ipInput.Octet1), Is.True);
                Assert.That(ipInput.Octet1.Font.SizeInPoints, Is.EqualTo(ipInput.Font.SizeInPoints));
                Assert.That(ipInput.Height, Is.EqualTo(InputControlMetrics.InputHeight(ipInput.Font.Height)));
                Assert.That(ipInput.Octet1.Margin, Is.EqualTo(Padding.Empty));
                Assert.That(ipInput.Octet1.Padding, Is.EqualTo(Padding.Empty));
                Assert.That(ipInput.Octet1.TextAlign, Is.EqualTo(HorizontalAlignment.Center));
            });
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void NativeSmallDefaultFontDoesNotMakeAnInputUseTheSmallTypographyRole()
        {
            using TextBox textBox = new()
            {
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 8.25f)
            };

            UiScaleManager.Instance.Apply(textBox);

            Assert.That(textBox.Font.SizeInPoints,
                Is.EqualTo(UiScaleManager.Instance.CreateFont(UiTypographyRole.Body).SizeInPoints));
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void HoverRestoresTheFontOfTheNativeNumericEditor()
        {
            using TestableNumericUpDown numericUpDown = new();
            _ = numericUpDown.Handle;
            UiScaleManager.Instance.Apply(numericUpDown);
            TextBoxBase nativeEditor = numericUpDown.Controls.OfType<TextBoxBase>().Single();
            nativeEditor.Font = new Font(nativeEditor.Font.FontFamily, 7f, nativeEditor.Font.Style);

            numericUpDown.RaiseMouseEnter();

            Assert.That(nativeEditor.Font.SizeInPoints, Is.EqualTo(numericUpDown.Font.SizeInPoints));
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void AbsoluteTableRowsExpandToFitScaledInputs()
        {
            using TableLayoutPanel table = new()
            {
                RowCount = 1,
                ColumnCount = 1,
                AutoSize = true
            };
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
            using MrngTextBox input = new() { Margin = new Padding(3) };
            table.Controls.Add(input, 0, 0);

            UiScaleManager.Instance.Apply(table);

            Assert.That(table.RowStyles[0].Height,
                Is.GreaterThanOrEqualTo(input.Height + input.Margin.Vertical));
        }

        private sealed class TestableTextBox : MrngTextBox
        {
            public void RaiseMouseEnter() => OnMouseEnter(System.EventArgs.Empty);
        }

        private sealed class TestableNumericUpDown : MrngNumericUpDown
        {
            public void RaiseMouseEnter() => OnMouseEnter(System.EventArgs.Empty);
        }

        private sealed class TestableComboBox : MrngComboBox
        {
            public void RaiseMouseEnter() => OnMouseEnter(System.EventArgs.Empty);
        }
    }
}
