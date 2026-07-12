using System;
using System.Drawing;
using System.Windows.Forms;
using LoipvRemote.App;
using LoipvRemote.UI.DesignSystem;

namespace LoipvRemote.UI.Forms
{
    internal sealed class UnifiedWindowHeader : Panel
    {
        private const double HeaderHeightReductionFactor = 0.8d;
        private const int MinimumHeaderHeight = 35;
        private const int MinimumMenuHostWidth = 400;
        private readonly FrmMain _owner;
        private readonly Panel _dragArea;
        private readonly Panel _menuHost;
        private readonly FlowLayoutPanel _windowButtons;
        private readonly MenuStrip _mainMenu;
        private readonly ToolStripLabel _appMenuIcon;
        private readonly WindowCaptionButton _minimizeButton;
        private readonly WindowCaptionButton _maximizeRestoreButton;
        private readonly WindowCaptionButton _closeButton;
        private readonly UnifiedHeaderMenuRenderer _menuRenderer = new();
        private Color _borderColor = Color.FromArgb(229, 229, 229);

        internal UnifiedWindowHeader(FrmMain owner, MenuStrip mainMenu)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            ArgumentNullException.ThrowIfNull(mainMenu);

            Dock = DockStyle.Top;
            BackColor = Color.FromArgb(247, 247, 248);
            Padding = new Padding(0, 0, 0, 1);
            TabStop = false;

            _windowButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 138,
                BackColor = BackColor,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _minimizeButton = new WindowCaptionButton(WindowCaptionButtonKind.Minimize)
            {
                AccessibleName = "Thu nhỏ cửa sổ"
            };
            _minimizeButton.Click += (_, _) => _owner.WindowState = FormWindowState.Minimized;

            _maximizeRestoreButton = new WindowCaptionButton(WindowCaptionButtonKind.Maximize)
            {
                AccessibleName = "Phóng to hoặc khôi phục cửa sổ"
            };
            _maximizeRestoreButton.Click += (_, _) => ToggleMaximizeRestore();

            _closeButton = new WindowCaptionButton(WindowCaptionButtonKind.Close)
            {
                AccessibleName = "Đóng cửa sổ"
            };
            _closeButton.Click += (_, _) => _owner.Close();

            WindowCaptionButton[] buttons = [_minimizeButton, _maximizeRestoreButton, _closeButton];
            foreach (WindowCaptionButtonKind kind in WindowCaptionButtonOrder.Standard)
                _windowButtons.Controls.Add(Array.Find(buttons, button => button.Kind == kind)!);

            _menuHost = new Panel
            {
                Dock = DockStyle.Left,
                Width = 310,
                BackColor = BackColor,
                Padding = Padding.Empty
            };
            mainMenu.Parent?.Controls.Remove(mainMenu);
            _mainMenu = mainMenu;
            mainMenu.Dock = DockStyle.Fill;
            mainMenu.AutoSize = false;
            mainMenu.GripStyle = ToolStripGripStyle.Hidden;
            mainMenu.Stretch = true;
            mainMenu.Padding = new Padding(0);
            mainMenu.ImageScalingSize = new Size(22, 22);
            mainMenu.BackColor = BackColor;
            mainMenu.RenderMode = ToolStripRenderMode.Professional;
            mainMenu.Renderer = _menuRenderer;
            _appMenuIcon = new ToolStripLabel
            {
                AccessibleName = "LoipvRemote",
                DisplayStyle = ToolStripItemDisplayStyle.Image,
                Image = Properties.Resources.LoipvRemote_Icon.ToBitmap(),
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                ToolTipText = "LoipvRemote"
            };
            mainMenu.Items.Insert(0, _appMenuIcon);
            foreach (ToolStripItem item in mainMenu.Items)
            {
                item.Margin = Padding.Empty;
                item.Padding = ReferenceEquals(item, _appMenuIcon)
                    ? Padding.Empty
                    : new Padding(12, 0, 12, 0);
            }
            mainMenu.MouseDoubleClick += MainMenuOnMouseDoubleClick;
            _menuHost.Controls.Add(mainMenu);

            _dragArea = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                AccessibleName = "Kéo cửa sổ"
            };

            Controls.Add(_dragArea);
            Controls.Add(_menuHost);
            Controls.Add(_windowButtons);

            _owner.Resize += (_, _) => UpdateMaximizeRestoreGlyph();
            _owner.DpiChanged += (_, _) => RefreshMetrics();
            Layout += (_, _) => LayoutHeaderControls();
            _dragArea.MouseDown += CaptionAreaOnMouseDown;
            _dragArea.DoubleClick += (_, _) => ToggleMaximizeRestore();

            UiScaleManager.Instance.Changed += (_, _) => RefreshMetrics();
            RefreshMetrics();
            UpdateMaximizeRestoreGlyph();
        }

        internal bool IsCaptionPoint(Point formClientPoint)
        {
            if (!Visible || !Bounds.Contains(formClientPoint)) return false;
            Point headerPoint = PointToClient(formClientPoint);
            return _dragArea.Bounds.Contains(headerPoint);
        }

        internal void ApplyTheme(Color background, Color foreground)
        {
            bool isDark = background.GetBrightness() < 0.5f;
            Color headerBackground = isDark ? background : Color.FromArgb(247, 247, 248);
            Color headerForeground = isDark ? foreground : Color.FromArgb(32, 33, 35);
            Color hoverBackground = isDark ? ControlPaint.Light(headerBackground) : Color.FromArgb(236, 236, 238);
            _borderColor = isDark ? ControlPaint.Light(headerBackground) : Color.FromArgb(229, 229, 229);

            BackColor = headerBackground;
            ForeColor = headerForeground;
            ApplyThemeToChildren(this, headerBackground, headerForeground);
            _menuRenderer.UpdateColors(headerBackground, headerForeground, hoverBackground);
            foreach (WindowCaptionButton button in new[] { _minimizeButton, _maximizeRestoreButton, _closeButton })
                button.SetPalette(headerBackground, headerForeground, hoverBackground, Color.FromArgb(232, 17, 35));
            Invalidate();
        }

        private static void ApplyThemeToChildren(Control control, Color background, Color foreground)
        {
            control.BackColor = background;
            control.ForeColor = foreground;
            foreach (Control child in control.Controls)
                ApplyThemeToChildren(child, background, foreground);
        }

        private void RefreshMetrics()
        {
            UiMetrics metrics = UiScaleManager.Instance.Metrics;
            int uncompressedHeight = Math.Max(44, (int)Math.Ceiling(metrics.InteractiveHeight * metrics.FontScale + 4));
            int baseHeight = ReduceHeaderHeight(uncompressedHeight);
            Height = metrics.ScaleForDpi(baseHeight, _owner.DeviceDpi / 96f);
            int buttonWidth = Math.Max(46, Height);
            _windowButtons.Width = buttonWidth * 3;
            _menuHost.Width = MenuHostWidthFor(Height);
            LayoutHeaderControls();
            Invalidate();
        }

        internal static int ReduceHeaderHeight(int uncompressedHeight)
        {
            if (uncompressedHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(uncompressedHeight));

            return Math.Max(MinimumHeaderHeight,
                (int)Math.Round(uncompressedHeight * HeaderHeightReductionFactor, MidpointRounding.AwayFromZero));
        }

        internal static int MenuHostWidthFor(int headerHeight)
        {
            if (headerHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(headerHeight));

            // The main menu contains the app icon plus four localized menu items.
            // Keep a stable minimum so the last item cannot overflow when the
            // compact header is enabled or Vietnamese labels are active.
            return Math.Max(MinimumMenuHostWidth, headerHeight * 8);
        }

        private void LayoutHeaderControls()
        {
            int contentHeight = Math.Max(1, ClientSize.Height - Padding.Bottom);
            int buttonWidth = Math.Max(46, _windowButtons.Width / 3);
            _mainMenu.Height = contentHeight;
            _appMenuIcon.AutoSize = false;
            _appMenuIcon.Size = new Size(contentHeight, contentHeight);
            foreach (Control button in _windowButtons.Controls)
                button.Size = new Size(buttonWidth, contentHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using Pen borderPen = new(_borderColor);
            e.Graphics.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);
        }

        private void CaptionAreaOnMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _owner.WindowState == FormWindowState.Maximized) return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(_owner.Handle, NativeMethods.WM_NCLBUTTONDOWN,
                                      (IntPtr)WindowChromeHitTest.Caption, IntPtr.Zero);
        }

        internal void ToggleMaximizeRestore()
        {
            _owner.WindowState = _owner.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }

        private void MainMenuOnMouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (_mainMenu.GetItemAt(e.Location) is null)
                ToggleMaximizeRestore();
        }

        private void UpdateMaximizeRestoreGlyph()
        {
            _maximizeRestoreButton.Kind = _owner.WindowState == FormWindowState.Maximized
                ? WindowCaptionButtonKind.Restore
                : WindowCaptionButtonKind.Maximize;
        }
    }

    internal enum WindowCaptionButtonKind
    {
        Minimize,
        Maximize,
        Restore,
        Close
    }

    internal static class WindowCaptionButtonOrder
    {
        internal static WindowCaptionButtonKind[] Standard { get; } =
        [
            WindowCaptionButtonKind.Minimize,
            WindowCaptionButtonKind.Maximize,
            WindowCaptionButtonKind.Close
        ];
    }

    internal sealed class WindowCaptionButton : Button
    {
        private Color _normalBackground = Color.FromArgb(247, 247, 248);
        private Color _normalForeground = Color.FromArgb(32, 33, 35);
        private Color _hoverBackground = Color.FromArgb(236, 236, 238);
        private Color _closeHoverBackground = Color.FromArgb(232, 17, 35);

        internal WindowCaptionButtonKind Kind { get; set; }

        internal WindowCaptionButton(WindowCaptionButtonKind kind)
        {
            Kind = kind;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TabStop = false;
            UseVisualStyleBackColor = false;
            Width = 46;
            Margin = Padding.Empty;
        }

        internal void SetPalette(Color normalBackground, Color normalForeground, Color hoverBackground, Color closeHoverBackground)
        {
            _normalBackground = normalBackground;
            _normalForeground = normalForeground;
            _hoverBackground = hoverBackground;
            _closeHoverBackground = closeHoverBackground;
            BackColor = normalBackground;
            ForeColor = normalForeground;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            Color color = Enabled ? ForeColor : SystemColors.GrayText;
            using Pen pen = new(color, 1.5f);
            Rectangle bounds = ClientRectangle;
            int centerX = bounds.Width / 2;
            int centerY = bounds.Height / 2;

            switch (Kind)
            {
                case WindowCaptionButtonKind.Minimize:
                    e.Graphics.DrawLine(pen, centerX - 6, centerY + 4, centerX + 6, centerY + 4);
                    break;
                case WindowCaptionButtonKind.Maximize:
                    e.Graphics.DrawRectangle(pen, centerX - 6, centerY - 5, 12, 10);
                    break;
                case WindowCaptionButtonKind.Restore:
                    e.Graphics.DrawRectangle(pen, centerX - 4, centerY - 6, 10, 9);
                    e.Graphics.DrawRectangle(pen, centerX - 6, centerY - 3, 10, 9);
                    break;
                case WindowCaptionButtonKind.Close:
                    e.Graphics.DrawLine(pen, centerX - 5, centerY - 5, centerX + 5, centerY + 5);
                    e.Graphics.DrawLine(pen, centerX + 5, centerY - 5, centerX - 5, centerY + 5);
                    break;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            BackColor = Kind == WindowCaptionButtonKind.Close ? _closeHoverBackground : _hoverBackground;
            ForeColor = Kind == WindowCaptionButtonKind.Close ? Color.White : _normalForeground;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            BackColor = _normalBackground;
            ForeColor = _normalForeground;
        }
    }

    internal sealed class UnifiedHeaderMenuRenderer : ToolStripProfessionalRenderer
    {
        private Color _background = Color.FromArgb(247, 247, 248);
        private Color _foreground = Color.FromArgb(32, 33, 35);
        private Color _hover = Color.FromArgb(236, 236, 238);

        internal void UpdateColors(Color background, Color foreground, Color hover)
        {
            _background = background;
            _foreground = foreground;
            _hover = hover;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(_background);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            using SolidBrush brush = new(e.Item.Selected || e.Item.Pressed ? _hover : _background);
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = _foreground;
            base.OnRenderItemText(e);
        }
    }
}
