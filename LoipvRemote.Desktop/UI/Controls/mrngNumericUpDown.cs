using System;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.Themes;

// ReSharper disable LocalizableElement

namespace LoipvRemote.UI.Controls
{
    [SupportedOSPlatform("windows")]
    //Repaint of the NumericUpDown, the composite control buttons are replaced because the
    //original ones cannot be themed due to protected inheritance
    internal class MrngNumericUpDown : NumericUpDown
    {
        private readonly ThemeManager _themeManager;
        private MrngButton? Up;
        private MrngButton? Down;

        public MrngNumericUpDown()
        {
            InitializeComponent();
            _themeManager = ThemeManager.getInstance();
            ThemeManager.getInstance().ThemeChanged += OnCreateControl;
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            if (!_themeManager.ActiveAndExtended) return;
            ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Foreground");
            BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Background");
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            if (Controls.Count > 0)
            {
                for (int i = 0; i < Controls.Count; i++)
                {
                    //Remove those non-themable buttons
                    if (Controls[i].GetType().ToString().Equals("System.Windows.Forms.UpDownBase+UpDownButtons"))
                        Controls.Remove(Controls[i]);

                    /* This is a bit of a hack.
                     * But if we have the buttons that we created already, redraw/return and don't add any more...
                     *
                     * OptionsPages are an example where the control is potentially created twice:
                     * AddOptionsPagesToListView and then LstOptionPages_SelectedIndexChanged
                     */
                    if (!(Controls[i] is MrngButton)) continue;
                    if (!Controls[i].Text.Equals("\u25B2") && !Controls[i].Text.Equals("\u25BC")) continue;
                    Invalidate();
                    return;
                }
            }

            //Add new themable buttons
            Up = new MrngButton
            {
                Text = "\u25B2",
                Font = new Font(Font.FontFamily, Math.Max(7f, Font.SizeInPoints * 0.7f))
            };
            Up.Click += Up_Click;
            Down = new MrngButton
            {
                Text = "\u25BC",
                Font = new Font(Font.FontFamily, Math.Max(7f, Font.SizeInPoints * 0.7f))
            };
            Down.Click += Down_Click;
            Controls.Add(Up);
            Controls.Add(Down);
            LayoutButtons();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutButtons();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (Up is not null) Up.Font = new Font(Font.FontFamily, Math.Max(7f, Font.SizeInPoints * 0.7f));
            if (Down is not null) Down.Font = new Font(Font.FontFamily, Math.Max(7f, Font.SizeInPoints * 0.7f));
        }

        private void LayoutButtons()
        {
            if (Up is null || Down is null) return;

            int buttonWidth = Math.Clamp(Height - 4, 18, 24);
            int buttonHeight = Math.Max(1, (Height - 3) / 2);
            int left = Math.Max(0, Width - buttonWidth - 1);
            Up.SetBounds(left, 1, buttonWidth, buttonHeight);
            Down.SetBounds(left, 1 + buttonHeight, buttonWidth, Math.Max(1, Height - buttonHeight - 2));
        }

        private void Down_Click(object sender, EventArgs e)
        {
            DownButton();
        }

        private void Up_Click(object sender, EventArgs e)
        {
            UpButton();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            if (_themeManager.ActiveAndExtended)
            {
                if (Enabled)
                {
                    ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Foreground");
                    BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Background");
                }
                else
                {
                    BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Disabled_Background");
                }
            }

            base.OnEnabledChanged(e);
            Invalidate();
        }


        //Redrawing border
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!_themeManager.ActiveAndExtended) return;
            //Fix Border
            if (BorderStyle != BorderStyle.None)
                e.Graphics.DrawRectangle(
                                         new Pen(_themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Border"),
                                                 1), 0, 0, Width - 1,
                                         Height - 1);
        }

        private void InitializeComponent()
        {
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            this.SuspendLayout();
            //
            // NGNumericUpDown
            //
            this.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular,
                                                System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
