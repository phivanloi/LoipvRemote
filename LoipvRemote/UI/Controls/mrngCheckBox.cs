using System;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.Themes;
using LoipvRemote.UI.DesignSystem;

namespace LoipvRemote.UI.Controls
{
    [SupportedOSPlatform("windows")]
    //Extended CheckBox class, the NGCheckBox onPaint completely repaint the control

    //
    // If this causes design issues in the future, may want to think about migrating to
    // CheckBoxRenderer:
    // https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.checkboxrenderer?view=netframework-4.6
    //
    public class MrngCheckBox : CheckBox
    {
        private ThemeManager _themeManager;
        public MrngCheckBox()
        {
            InitializeComponent();
            ThemeManager.getInstance().ThemeChanged += OnCreateControl;
        }

        public enum MouseState
        {
            HOVER,
            DOWN,
            OUT
        }

        public MouseState _mice { get; set; }


        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            _themeManager = ThemeManager.getInstance();
            if (!_themeManager.ThemingActive) return;
            _mice = MouseState.OUT;
            MouseEnter += (sender, args) =>
            {
                _mice = MouseState.HOVER;
                Invalidate();
            };
            MouseLeave += (sender, args) =>
            {
                _mice = MouseState.OUT;
                Invalidate();
            };
            MouseDown += (sender, args) =>
            {
                if (args.Button != MouseButtons.Left) return;
                _mice = MouseState.DOWN;
                Invalidate();
            };
            MouseUp += (sender, args) =>
            {
                _mice = MouseState.OUT;

                Invalidate();
            };

            Invalidate();
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            if (!_themeManager.ActiveAndExtended)
            {
                base.OnPaint(e);
                return;
            }

            //Get the colors
            Color fore;
            Color glyph;
            Color checkBorder;

            Color back = _themeManager.ActiveTheme.ExtendedPalette.getColor("CheckBox_Background");
            if (Enabled)
            {
                glyph = _themeManager.ActiveTheme.ExtendedPalette.getColor("CheckBox_Glyph");
                fore = _themeManager.ActiveTheme.ExtendedPalette.getColor("CheckBox_Text");
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (_mice)
                {
                    case MouseState.HOVER:
                        checkBorder = _themeManager.ActiveTheme.ExtendedPalette.getColor("CheckBox_Border_Hover");
                        break;
                    case MouseState.DOWN:
                        checkBorder = _themeManager.ActiveTheme.ExtendedPalette.getColor("CheckBox_Border_Pressed");
                        break;
                    default:
                        checkBorder = _themeManager.ActiveTheme.ExtendedPalette.getColor("CheckBox_Border");
                        break;
                }
            }
            else
            {
                fore = _themeManager.ActiveTheme.ExtendedPalette.getColor("CheckBox_Text_Disabled");
                glyph = _themeManager.ActiveTheme.ExtendedPalette.getColor("CheckBox_Glyph_Disabled");
                checkBorder = _themeManager.ActiveTheme.ExtendedPalette.getColor("CheckBox_Border_Disabled");
            }

            Color parentBackColor = Parent?.BackColor ?? BackColor;
            e.Graphics.Clear(parentBackColor);

            int glyphSize = InputControlMetrics.CheckBoxGlyphSize(Font.Height);
            Size checkboxSize = new(glyphSize, glyphSize);
            int checkboxYCoord = Math.Max(0, (Height - checkboxSize.Height) / 2);
            int textXCoord = checkboxSize.Width + 8;
            Rectangle boxRect = new(0, checkboxYCoord, checkboxSize.Width, checkboxSize.Height);

            using (Pen p = new(checkBorder))
            {
                e.Graphics.FillRectangle(new SolidBrush(back), boxRect);
                e.Graphics.DrawRectangle(p, boxRect);
            }

            if (Checked)
            {
                float checkmarkPoints = Math.Max(8f, (glyphSize - 2) * 72f / e.Graphics.DpiY);
                using Font checkmarkFont = new("Segoe UI Symbol", checkmarkPoints, FontStyle.Regular, GraphicsUnit.Point);
                TextRenderer.DrawText(e.Graphics, "\u2713", checkmarkFont, boxRect, glyph,
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                      TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }

            Rectangle textRect = new(textXCoord, 0, Math.Max(0, Width - textXCoord), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect, fore, parentBackColor,
                                  TextFormatFlags.PathEllipsis | TextFormatFlags.VerticalCenter |
                                  TextFormatFlags.SingleLine);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            //
            // NGCheckBox
            //
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ResumeLayout(false);
        }
    }
}
