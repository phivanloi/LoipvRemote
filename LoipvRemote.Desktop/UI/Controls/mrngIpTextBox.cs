/* Source modified from here:
 * http://www.codeproject.com/Articles/11576/IP-TextBox
 * Original Author: mawnkay
 */

using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;
using LoipvRemote.Themes;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;
using LoipvRemote.UI.DesignSystem;

namespace LoipvRemote.UI.Controls
{
    [SupportedOSPlatform("windows")]
    /* class IPTextBox
     * An IP Address Box
     * A TextBox that only allows entry of a valid ip address
     */
    public class MrngIpTextBox : UserControl
    {
        private Panel panel1 = null!;
        public MrngTextBox Octet1 { get; private set; } = null!;
        public MrngTextBox Octet2 { get; private set; } = null!;
        public MrngTextBox Octet3 { get; private set; } = null!;
        public MrngTextBox Octet4 { get; private set; } = null!;
        private MrngLabel label1 = null!;
        private MrngLabel label2 = null!;
        private MrngLabel label3 = null!;
        private ToolTip toolTip1 = null!;
        private System.ComponentModel.Container components = null!;

        /* Sets and Gets the tooltiptext on toolTip1 */
        public string ToolTipText
        {
            get => toolTip1.GetToolTip(Octet1) ?? string.Empty;
            set
            {
                toolTip1.SetToolTip(Octet1, value);
                toolTip1.SetToolTip(Octet2, value);
                toolTip1.SetToolTip(Octet3, value);
                toolTip1.SetToolTip(Octet4, value);
                toolTip1.SetToolTip(label1, value);
                toolTip1.SetToolTip(label2, value);
                toolTip1.SetToolTip(label3, value);
            }
        }

        /* Set or Get the string that represents the value in the box */
        [AllowNull]
        public override string Text
        {
            get => (Octet1.Text ?? string.Empty) + @"." + (Octet2.Text ?? string.Empty) + @"." + (Octet3.Text ?? string.Empty) + @"." + (Octet4.Text ?? string.Empty);
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    string[] pieces = value.Split(@".".ToCharArray(), 4);
                    Octet1.Text = pieces.Length > 0 ? pieces[0] : string.Empty;
                    Octet2.Text = pieces.Length > 1 ? pieces[1] : string.Empty;
                    Octet3.Text = pieces.Length > 2 ? pieces[2] : string.Empty;
                    Octet4.Text = pieces.Length > 3 ? pieces[3] : string.Empty;
                }
                else
                {
                    Octet1.Text = string.Empty;
                    Octet2.Text = string.Empty;
                    Octet3.Text = string.Empty;
                    Octet4.Text = string.Empty;
                }
            }
        }

        /* Fix for CS8618: Initialize all non-nullable fields in the constructor to ensure they are not null. */

        public MrngIpTextBox()
        {
            // This call is required by the Windows.Forms Form Designer.
            InitializeComponent();
            SetTabSTopProperties();
            ApplyInputLayout();
        }

        private void SetTabSTopProperties()
        {
            Octet1.TabIndex = 0;
            Octet2.TabIndex = 1;
            Octet3.TabIndex = 2;
            Octet4.TabIndex = 3;
            Octet1.TabStop = true;
            Octet2.TabStop = true;
            Octet3.TabStop = true;
            Octet4.TabStop = true;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyTheme();
            ThemeManager.getInstance().ThemeChanged += ApplyTheme;
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            ApplyInputLayout();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyInputLayout();
        }

        private void ApplyInputLayout()
        {
            if (panel1 == null || Octet1 == null || Octet2 == null || Octet3 == null || Octet4 == null)
                return;

            int inputHeight = InputControlMetrics.InputHeight(Font.Height);
            int dotWidth = Math.Max(6, TextRenderer.MeasureText(".", Font, Size.Empty,
                                                                 TextFormatFlags.NoPadding).Width + 2);
            int octetWidth = Math.Max(24, (Math.Max(124, Width) - 4 - dotWidth * 3) / 4);
            int requiredWidth = octetWidth * 4 + dotWidth * 3 + 4;

            MinimumSize = new Size(requiredWidth, inputHeight);
            if (Width < requiredWidth) Width = requiredWidth;
            if (Height != inputHeight) Height = inputHeight;
            panel1.Bounds = new Rectangle(0, 0, Width, inputHeight);

            MrngTextBox[] octets = [Octet1, Octet2, Octet3, Octet4];
            MrngLabel[] separators = [label3, label1, label2];
            int left = 2;
            for (int index = 0; index < octets.Length; index++)
            {
                MrngTextBox octet = octets[index];
                octet.Font = Font;
                octet.AutoSize = false;
                octet.Margin = Padding.Empty;
                octet.Padding = Padding.Empty;
                octet.TextAlign = HorizontalAlignment.Center;
                octet.Bounds = new Rectangle(left, 0, octetWidth, inputHeight);
                left += octetWidth;

                if (index >= separators.Length) continue;
                MrngLabel separator = separators[index];
                separator.Font = Font;
                separator.AutoSize = false;
                separator.Margin = Padding.Empty;
                separator.TextAlign = ContentAlignment.MiddleCenter;
                separator.Bounds = new Rectangle(left, 0, dotWidth, inputHeight);
                left += dotWidth;
            }
        }

        private void ApplyTheme()
        {
            if (!ThemeManager.getInstance().ActiveAndExtended) return;
            panel1.BackColor = ThemeManager.getInstance().ActiveTheme.ExtendedPalette.getColor("TextBox_Background");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.panel1 = new System.Windows.Forms.Panel();
            this.Octet4 = new LoipvRemote.UI.Controls.MrngTextBox();
            this.Octet3 = new LoipvRemote.UI.Controls.MrngTextBox();
            this.Octet2 = new LoipvRemote.UI.Controls.MrngTextBox();
            this.Octet1 = new LoipvRemote.UI.Controls.MrngTextBox();
            this.label2 = new LoipvRemote.UI.Controls.MrngLabel();
            this.label1 = new LoipvRemote.UI.Controls.MrngLabel();
            this.label3 = new LoipvRemote.UI.Controls.MrngLabel();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            //
            // panel1
            //
            this.panel1.BackColor = System.Drawing.SystemColors.Window;
            this.panel1.Controls.Add(this.Octet4);
            this.panel1.Controls.Add(this.Octet3);
            this.panel1.Controls.Add(this.Octet2);
            this.panel1.Controls.Add(this.Octet1);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.label3);
            this.panel1.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(124, 18);
            this.panel1.TabIndex = 0;
            //
            // Octet4
            //
            this.Octet4.BackColor = System.Drawing.SystemColors.Menu;
            this.Octet4.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.Octet4.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.Octet4.Location = new System.Drawing.Point(95, 1);
            this.Octet4.MaxLength = 3;
            this.Octet4.Name = "Octet4";
            this.Octet4.Size = new System.Drawing.Size(24, 16);
            this.Octet4.TabIndex = 4;
            this.Octet4.TabStop = false;
            this.Octet4.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.Octet4.Enter += new System.EventHandler(this.Box_Enter);
            this.Octet4.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.Box4_KeyPress);
            //
            // Octet3
            //
            this.Octet3.BackColor = System.Drawing.SystemColors.Menu;
            this.Octet3.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.Octet3.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.Octet3.Location = new System.Drawing.Point(63, 1);
            this.Octet3.MaxLength = 3;
            this.Octet3.Name = "Octet3";
            this.Octet3.Size = new System.Drawing.Size(24, 16);
            this.Octet3.TabIndex = 3;
            this.Octet3.TabStop = false;
            this.Octet3.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.Octet3.Enter += new System.EventHandler(this.Box_Enter);
            this.Octet3.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.Box3_KeyPress);
            //
            // Octet2
            //
            this.Octet2.BackColor = System.Drawing.SystemColors.Menu;
            this.Octet2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.Octet2.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.Octet2.Location = new System.Drawing.Point(32, 1);
            this.Octet2.MaxLength = 3;
            this.Octet2.Name = "Octet2";
            this.Octet2.Size = new System.Drawing.Size(24, 16);
            this.Octet2.TabIndex = 2;
            this.Octet2.TabStop = false;
            this.Octet2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.Octet2.Enter += new System.EventHandler(this.Box_Enter);
            this.Octet2.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.Box2_KeyPress);
            //
            // Octet1
            //
            this.Octet1.BackColor = System.Drawing.SystemColors.Menu;
            this.Octet1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.Octet1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular,
                                                       System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Octet1.Location = new System.Drawing.Point(1, 1);
            this.Octet1.MaxLength = 3;
            this.Octet1.Name = "Octet1";
            this.Octet1.Size = new System.Drawing.Size(24, 16);
            this.Octet1.TabIndex = 1;
            this.Octet1.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.Octet1.Enter += new System.EventHandler(this.Box_Enter);
            this.Octet1.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.Box1_KeyPress);
            //
            // label2
            //
            this.label2.Location = new System.Drawing.Point(86, 2);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(8, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = ".";
            //
            // label1
            //
            this.label1.Location = new System.Drawing.Point(55, 2);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(8, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = ".";
            //
            // label3
            //
            this.label3.Location = new System.Drawing.Point(23, 1);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(8, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = ".";
            //
            // IPTextBox
            //
            this.Controls.Add(this.panel1);
            this.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular,
                                                System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "IPTextBox";
            this.Size = new System.Drawing.Size(124, 18);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        /* IsValid(string inString)
		 * Checks that a string passed in resolves to an integer value between 0 and 255
		 * param inString: The string passed in for testing
		 * return: True if the string is between 0 and 255 inclusively, false otherwise
		 * endif
		 */
        private static bool IsValid(string inString)
        {
            try
            {
                int theValue = int.Parse(inString, CultureInfo.InvariantCulture);
                if (theValue >= 0 && theValue <= 255)
                    return true;

                MessageBox.Show(Language.MustBeBetween0And255, Language.OutOfRange);
                return false;
            }
            catch
            {
                return false;
            }
        }

        /* Update the method signatures to include nullable reference type annotations
         * to match the nullability of the target delegate 'KeyPressEventHandler'.
         */

        private void Box1_KeyPress(object? sender, KeyPressEventArgs e)
        {
            //Only Accept a '.', a numeral, or backspace
            if (e.KeyChar.ToString() == "." || char.IsDigit(e.KeyChar) || e.KeyChar == 8)
            {
                //If the key pressed is a '.'
                if (e.KeyChar.ToString() == ".")
                {
                    //If the Text is a valid ip octet move to the next box
                    if (Octet1.Text != "" && Octet1.Text.Length != Octet1.SelectionLength)
                    {
                        if (IsValid(Octet1.Text))
                            Octet2.Focus();
                        else
                            Octet1.SelectAll();
                    }

                    e.Handled = true;
                }

                //If we are not overwriting the whole text
                else if (Octet1.SelectionLength != Octet1.Text.Length)
                {
                    //Check that the new Text value will be a valid
                    // ip octet then move on to next box
                    if (Octet1.Text.Length != 2) return;
                    if (!IsValid(Octet1.Text + e.KeyChar))
                    {
                        Octet1.SelectAll();
                        e.Handled = true;
                    }
                    else
                    {
                        Octet2.Focus();
                    }
                }
            }
            //Do nothing if the keypress is not numeral, backspace, or '.'
            else
                e.Handled = true;
        }

        /* Performs KeyPress analysis and handling to ensure a valid ip octet is
         * being entered in Box2.
         */
        private void Box2_KeyPress(object? sender, KeyPressEventArgs e)
        {
            //Similar to Box1_KeyPress but in special case for backspace moves cursor
            //to the previous box (Box1)
            if (e.KeyChar.ToString() == "." || char.IsDigit(e.KeyChar) || e.KeyChar == 8)
            {
                if (e.KeyChar.ToString() == ".")
                {
                    if (Octet2.Text != "" && Octet2.Text.Length != Octet2.SelectionLength)
                    {
                        if (IsValid(Octet1.Text))
                            Octet3.Focus();
                        else
                            Octet2.SelectAll();
                    }

                    e.Handled = true;
                }
                else if (Octet2.SelectionLength != Octet2.Text.Length)
                {
                    if (Octet2.Text.Length != 2) return;
                    if (!IsValid(Octet2.Text + e.KeyChar))
                    {
                        Octet2.SelectAll();
                        e.Handled = true;
                    }
                    else
                    {
                        Octet3.Focus();
                    }
                }
                else if (Octet2.Text.Length == 0 && e.KeyChar == 8)
                {
                    Octet1.Focus();
                    Octet1.SelectionStart = Octet1.Text.Length;
                }
            }
            else
                e.Handled = true;
        }

        /* Performs KeyPress analysis and handling to ensure a valid ip octet is
         * being entered in Box3.
         */
        private void Box3_KeyPress(object? sender, KeyPressEventArgs e)
        {
            //Identical to Box2_KeyPress except that previous box is Box2 and
            //next box is Box3
            if (e.KeyChar.ToString() == "." || char.IsDigit(e.KeyChar) || e.KeyChar == 8)
            {
                if (e.KeyChar.ToString() == ".")
                {
                    if (Octet3.Text != "" && Octet3.SelectionLength != Octet3.Text.Length)
                    {
                        if (IsValid(Octet1.Text))
                            Octet4.Focus();
                        else
                            Octet3.SelectAll();
                    }

                    e.Handled = true;
                }
                else if (Octet3.SelectionLength != Octet3.Text.Length)
                {
                    if (Octet3.Text.Length != 2) return;
                    if (!IsValid(Octet3.Text + e.KeyChar))
                    {
                        Octet3.SelectAll();
                        e.Handled = true;
                    }
                    else
                    {
                        Octet4.Focus();
                    }
                }
                else if (Octet3.Text.Length == 0 && e.KeyChar == 8)
                {
                    Octet2.Focus();
                    Octet2.SelectionStart = Octet2.Text.Length;
                }
            }
            else
                e.Handled = true;
        }

        /* Performs KeyPress analysis and handling to ensure a valid ip octet is
         * being entered in Box4.
         */
        private void Box4_KeyPress(object? sender, KeyPressEventArgs e)
        {
            //Similar to Box3 but ignores the '.' character and does not advance
            //to the next box.  Also Box3 is previous box for backspace case.
            if (char.IsDigit(e.KeyChar) || e.KeyChar == 8)
            {
                if (Octet4.SelectionLength != Octet4.Text.Length)
                {
                    if (Octet4.Text.Length != 2) return;
                    if (IsValid(Octet4.Text + e.KeyChar)) return;
                    Octet4.SelectAll();
                    e.Handled = true;
                }
                else if (Octet4.Text.Length == 0 && e.KeyChar == 8)
                {
                    Octet3.Focus();
                    Octet3.SelectionStart = Octet3.Text.Length;
                }
            }
            else
                e.Handled = true;
        }

        // Update the method signature to include nullable reference type annotations
        private void Box_Enter(object? sender, EventArgs e)
        {
            TextBox? tb = sender as TextBox;
            tb?.SelectAll();
        }
    }
}
