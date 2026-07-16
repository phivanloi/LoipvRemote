using System;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.UI.Controls;

namespace LoipvRemote.UI.DesignSystem
{
    [SupportedOSPlatform("windows")]
    public sealed class UiScaleManager
    {
        private sealed class TypographyState(UiTypographyRole role, FontStyle style)
        {
            public UiTypographyRole Role { get; } = role;
            public FontStyle Style { get; } = style;
        }

        private sealed class InputTypographyBinding
        {
            public bool IsRestoring { get; set; }
        }

        private sealed class NativeInputEditorBinding
        {
            public bool IsRestoring { get; set; }
        }

        private static readonly Lazy<UiScaleManager> LazyInstance = new(() => new UiScaleManager());
        private readonly ConditionalWeakTable<Control, TypographyState> _typographyStates = new();
        private readonly ConditionalWeakTable<Control, InputTypographyBinding> _inputTypographyBindings = new();
        private readonly ConditionalWeakTable<Control, NativeInputEditorBinding> _nativeInputEditorBindings = new();

        private UiScaleManager()
        {
            Preferences = UiPreferences.FromSettings();
            Metrics = new UiMetrics(Preferences);
        }

        public static UiScaleManager Instance => LazyInstance.Value;
        public UiPreferences Preferences { get; private set; }
        public UiMetrics Metrics { get; private set; }
        public event EventHandler? Changed;

        public Font CreateFont(UiTypographyRole role, FontStyle style = FontStyle.Regular)
        {
            string family = ResolveFontFamily(role);
            return new Font(family, Metrics.FontPoints(role), style, GraphicsUnit.Point);
        }

        public void RefreshFromSettings()
        {
            Preferences = UiPreferences.FromSettings();
            Metrics = new UiMetrics(Preferences);
            ApplyToOpenForms();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void ChangeFontScale(int deltaPercent)
        {
            Properties.OptionsAppearancePage settings = Properties.OptionsAppearancePage.Default;
            settings.UiFontScalePercent = Math.Clamp(settings.UiFontScalePercent + deltaPercent, 90, 150);
            settings.Save();
            RefreshFromSettings();
        }

        public void ResetFontScale()
        {
            Properties.OptionsAppearancePage settings = Properties.OptionsAppearancePage.Default;
            settings.UiFontScalePercent = UiPreferences.DefaultFontScalePercent;
            settings.Save();
            RefreshFromSettings();
        }

        public void Apply(Control root, bool recursive = true)
        {
            ArgumentNullException.ThrowIfNull(root);
            ApplyTypography(root);
            ApplyMetrics(root);
            if (!recursive || ShouldSkipChildren(root)) return;
            foreach (Control child in root.Controls)
                Apply(child);
            if (root is TableLayoutPanel tableLayout)
                ExpandAbsoluteRows(tableLayout);
            EnsureContainerContentFits(root);
        }

        public void ApplyToolStrip(ToolStrip toolStrip)
        {
            toolStrip.Font = CreateFont(UiTypographyRole.Body, toolStrip.Font.Style);
            int iconSize = UiMetrics.ScaleForDpi(Metrics.IconSize, Math.Max(96, toolStrip.DeviceDpi) / 96f);
            IconService.ApplyToToolStrip(toolStrip, iconSize);
        }

        private void ApplyToOpenForms()
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
            {
                if (form.IsDisposed) continue;
                if (form.InvokeRequired)
                    form.BeginInvoke(() => Apply(form));
                else
                    Apply(form);
            }
        }

        private void ApplyTypography(Control control)
        {
            TypographyState state = _typographyStates.GetValue(control,
                current => new TypographyState(InferRole(current), current.Font.Style));
            Font replacement = CreateFont(state.Role, state.Style);
            control.Font = replacement;
        }

        private void ApplyMetrics(Control control)
        {
            switch (control)
            {
                case ToolStrip strip:
                    ApplyToolStrip(strip);
                    break;
                case TextBox textBox when !textBox.Multiline:
                    KeepInputTypographyStable(textBox);
                    textBox.AutoSize = false;
                    textBox.Height = InputControlMetrics.InputHeight(textBox.Font.Height);
                    break;
                case ComboBox comboBox:
                    KeepInputTypographyStable(comboBox);
                    comboBox.Height = InputControlMetrics.InputHeight(comboBox.Font.Height);
                    comboBox.ItemHeight = InputControlMetrics.ComboBoxItemHeight(comboBox.Font.Height);
                    SynchronizeNativeInputEditorFont(comboBox);
                    break;
                case NumericUpDown numericUpDown:
                    KeepInputTypographyStable(numericUpDown);
                    numericUpDown.Height = InputControlMetrics.InputHeight(numericUpDown.Font.Height);
                    SynchronizeNativeInputEditorFont(numericUpDown);
                    break;
                case CheckBox checkBox:
                    checkBox.MinimumSize = new Size(0, Math.Max(20, checkBox.Font.Height + 4));
                    break;
                case TreeView tree:
                    tree.ItemHeight = ScaleForControlDpi(tree, Metrics.RowHeight);
                    break;
                case BrightIdeasSoftware.ObjectListView objectList:
                    objectList.RowHeight = ScaleForControlDpi(objectList, Metrics.RowHeight);
                    objectList.Font = CreateFont(UiTypographyRole.Body, objectList.Font.Style);
                    break;
                case ListView list:
                    list.Font = CreateFont(UiTypographyRole.Body, list.Font.Style);
                    break;
            }
        }

        private static int ScaleForControlDpi(Control control, int logicalPixels) =>
            UiMetrics.ScaleForDpi(logicalPixels, Math.Max(96, control.DeviceDpi) / 96f);

        private static void ExpandAbsoluteRows(TableLayoutPanel table)
        {
            // Fixed rows are authored for the 96-DPI designer font. Measure the
            // scaled content (including nested panels) before the next layout pass.
            for (int pass = 0; pass < 3; pass++)
            {
                table.PerformLayout();
                bool changed = false;
                int rowCount = Math.Min(table.RowStyles.Count, Math.Max(table.RowCount, 1));
                for (int row = 0; row < rowCount; row++)
                {
                    RowStyle style = table.RowStyles[row];
                    if (style.SizeType != SizeType.Absolute)
                        continue;

                    int requiredHeight = table.Controls.Cast<Control>()
                        .Where(control => table.GetRow(control) == row && table.GetRowSpan(control) == 1 && control.Visible)
                        .Select(control => GetDesiredHeight(control) + control.Margin.Vertical)
                        .DefaultIfEmpty(0)
                        .Max();
                    if (requiredHeight > style.Height)
                    {
                        style.Height = requiredHeight;
                        changed = true;
                    }
                }

                if (!changed)
                    break;
            }

            table.PerformLayout();
        }

        private static int GetDesiredHeight(Control control)
        {
            int desired = control switch
            {
                TextBox textBox when !textBox.Multiline => InputControlMetrics.InputHeight(textBox.Font.Height),
                ComboBox comboBox => InputControlMetrics.InputHeight(comboBox.Font.Height),
                NumericUpDown numericUpDown => InputControlMetrics.InputHeight(numericUpDown.Font.Height),
                CheckBox checkBox => Math.Max(20, checkBox.Font.Height + 4),
                RadioButton radioButton => Math.Max(20, radioButton.Font.Height + 4),
                _ => Math.Max(control.Height, control.GetPreferredSize(Size.Empty).Height)
            };

            foreach (Control child in control.Controls)
            {
                if (!child.Visible)
                    continue;

                int childBottom = child.Top + GetDesiredHeight(child) + child.Margin.Bottom;
                desired = Math.Max(desired, childBottom + control.Padding.Vertical);
            }

            return desired;
        }

        private static void EnsureContainerContentFits(Control container)
        {
            if (container is Form || container.Controls.Count == 0)
                return;

            int requiredHeight = container.Padding.Vertical;
            foreach (Control child in container.Controls)
            {
                if (!child.Visible)
                    continue;

                requiredHeight = Math.Max(requiredHeight,
                    child.Top + GetDesiredHeight(child) + child.Margin.Bottom + container.Padding.Bottom);
            }

            if (requiredHeight > container.MinimumSize.Height)
            {
                container.MinimumSize = new Size(container.MinimumSize.Width, requiredHeight);
                container.PerformLayout();
            }
        }

        private void KeepInputTypographyStable(Control input)
        {
            _inputTypographyBindings.GetValue(input, control =>
            {
                EventHandler restoreTypography = (_, _) => RestoreInputTypography(control);
                control.Enter += restoreTypography;
                control.MouseEnter += restoreTypography;
                control.FontChanged += restoreTypography;
                control.ControlAdded += (_, _) => SynchronizeNativeInputEditorFont(control);
                return new InputTypographyBinding();
            });
        }

        private void RestoreInputTypography(Control input)
        {
            if (input.IsDisposed) return;

            InputTypographyBinding binding = _inputTypographyBindings.GetValue(input,
                _ => new InputTypographyBinding());
            if (binding.IsRestoring) return;

            binding.IsRestoring = true;
            try
            {
                ApplyTypography(input);
                ApplyMetrics(input);
            }
            finally
            {
                binding.IsRestoring = false;
            }
        }

        private void SynchronizeNativeInputEditorFont(Control input)
        {
            foreach (Control child in input.Controls)
            {
                if (child is TextBoxBase)
                    KeepNativeInputEditorTypographyStable(child, input);

                SynchronizeNativeInputEditorFont(child);
            }
        }

        private void KeepNativeInputEditorTypographyStable(Control editor, Control owner)
        {
            NativeInputEditorBinding binding = _nativeInputEditorBindings.GetValue(editor, control =>
            {
                NativeInputEditorBinding result = new();
                control.FontChanged += (_, _) => RestoreNativeInputEditorFont(control, owner, result);
                return result;
            });

            RestoreNativeInputEditorFont(editor, owner, binding);
        }

        private static void RestoreNativeInputEditorFont(Control editor, Control owner,
                                                          NativeInputEditorBinding binding)
        {
            if (editor.IsDisposed || owner.IsDisposed || binding.IsRestoring || editor.Font.Equals(owner.Font))
                return;

            binding.IsRestoring = true;
            try
            {
                editor.Font = owner.Font;
            }
            finally
            {
                binding.IsRestoring = false;
            }
        }

        private string ResolveFontFamily(UiTypographyRole role)
        {
            if (role == UiTypographyRole.Monospace)
                return FontFamily.Families.Any(f => f.Name.Equals("Cascadia Mono", StringComparison.OrdinalIgnoreCase))
                    ? "Cascadia Mono"
                    : FontFamily.GenericMonospace.Name;

            if (!Preferences.FontFamily.Equals("System", StringComparison.OrdinalIgnoreCase) &&
                FontFamily.Families.Any(f => f.Name.Equals(Preferences.FontFamily, StringComparison.OrdinalIgnoreCase)))
                return Preferences.FontFamily;

            return (SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont).Name;
        }

        private static UiTypographyRole InferRole(Control control)
        {
            if (control is TextBoxBase or ComboBox or NumericUpDown)
                return UiTypographyRole.Body;

            Font font = control.Font;
            if (font.FontFamily.Name.Contains("Mono", StringComparison.OrdinalIgnoreCase) ||
                font.FontFamily.Name.Contains("Consolas", StringComparison.OrdinalIgnoreCase))
                return UiTypographyRole.Monospace;
            if (font.SizeInPoints <= 9.25f) return UiTypographyRole.Small;
            if (font.SizeInPoints <= 11f) return UiTypographyRole.Body;
            if (font.SizeInPoints <= 13f) return UiTypographyRole.Title;
            return UiTypographyRole.LargeTitle;
        }

        private static bool ShouldSkipChildren(Control control)
        {
            if (control is ComboBox or NumericUpDown or PropertyGrid or MrngIpTextBox)
                return true;

            string typeName = control.GetType().FullName ?? string.Empty;
            return typeName.Contains("AxMSTSCLib", StringComparison.OrdinalIgnoreCase) ||
                   typeName.Contains("VncSharp", StringComparison.OrdinalIgnoreCase) ||
                   typeName.Contains("ManagedTerminalControl", StringComparison.OrdinalIgnoreCase) ||
                   typeName.EndsWith("InterfaceControl", StringComparison.OrdinalIgnoreCase);
        }
    }
}
