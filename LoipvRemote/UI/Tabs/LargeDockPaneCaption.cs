using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using LoipvRemote.UI.DesignSystem;

namespace LoipvRemote.UI.Tabs
{
    /// <summary>
    /// Tool-window caption with larger, cropped dock and close glyphs for the
    /// application-wide 125% UI scale.
    /// </summary>
    [ToolboxItem(false)]
    internal sealed class LargeDockPaneCaption : DockPaneCaptionBase
    {
        private static int ButtonImageSize => UiScaleManager.Instance.Metrics.IconSize;
        private const int TextGapTop = 3;
        private const int TextGapBottom = 2;
        private const int TextGapLeft = 4;
        private const int TextGapRight = 4;
        private const int ButtonGapTop = 2;
        private const int ButtonGapBottom = 2;
        private const int ButtonGapBetween = 3;
        private const int ButtonGapRight = 4;

        private readonly ToolTip _toolTip = new();
        private InertButtonBase? _closeButton;
        private InertButtonBase? _autoHideButton;
        private InertButtonBase? _optionsButton;

        public LargeDockPaneCaption(DockPane pane) : base(pane)
        {
            UiScaleManager.Instance.Changed += UiScaleChanged;
            DockPane.DockStateChanged += DockPaneOnDockStateChanged;
        }

        private Font TextFont => DockPane.DockPanel.Theme.Skin.DockPaneStripSkin.TextFont;

        private InertButtonBase CloseButton => _closeButton ??= CreateButton(
            DockPane.DockPanel.Theme.ImageService.DockPaneHover_Close,
            DockPane.DockPanel.Theme.ImageService.DockPane_Close,
            DockPane.DockPanel.Theme.ImageService.DockPanePress_Close,
            DockPane.DockPanel.Theme.ImageService.DockPaneActiveHover_Close,
            DockPane.DockPanel.Theme.ImageService.DockPaneActive_Close,
            "Đóng", Close_Click);

        private InertButtonBase AutoHideButton => _autoHideButton ??= CreateButton(
            DockPane.DockPanel.Theme.ImageService.DockPaneHover_Dock,
            DockPane.DockPanel.Theme.ImageService.DockPane_Dock,
            DockPane.DockPanel.Theme.ImageService.DockPanePress_Dock,
            DockPane.DockPanel.Theme.ImageService.DockPaneActiveHover_Dock,
            DockPane.DockPanel.Theme.ImageService.DockPaneActive_Dock,
            "Ghim / tự ẩn", AutoHide_Click,
            DockPane.DockPanel.Theme.ImageService.DockPaneActiveHover_AutoHide,
            DockPane.DockPanel.Theme.ImageService.DockPaneActive_AutoHide,
            DockPane.DockPanel.Theme.ImageService.DockPanePress_AutoHide);

        private InertButtonBase OptionsButton => _optionsButton ??= CreateButton(
            DockPane.DockPanel.Theme.ImageService.DockPaneHover_Option,
            DockPane.DockPanel.Theme.ImageService.DockPane_Option,
            DockPane.DockPanel.Theme.ImageService.DockPanePress_Option,
            DockPane.DockPanel.Theme.ImageService.DockPaneActiveHover_Option,
            DockPane.DockPanel.Theme.ImageService.DockPaneActive_Option,
            "Tùy chọn", Options_Click);

        private InertButtonBase CreateButton(Bitmap hovered, Bitmap normal, Bitmap pressed, Bitmap hoveredActive,
            Bitmap active, string toolTip, EventHandler click, Bitmap? hoveredAutoHide = null,
            Bitmap? autoHide = null, Bitmap? pressedAutoHide = null)
        {
            InertButtonBase button = new LargeCaptionButton(this, hovered, normal, pressed, hoveredActive, active,
                hoveredAutoHide, autoHide, pressedAutoHide);
            _toolTip.SetToolTip(button, toolTip);
            button.Click += click;
            Controls.Add(button);
            return button;
        }

        protected override int MeasureHeight()
        {
            if (LeftSidebarDockingPolicy.UsesHiddenCaption(DockPane.DockState))
                return 0;

            int textHeight = TextFont.Height + TextGapTop + TextGapBottom;
            return Math.Max(textHeight, ButtonImageSize + ButtonGapTop + ButtonGapBottom);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (LeftSidebarDockingPolicy.UsesHiddenCaption(DockPane.DockState))
                return;

            base.OnPaint(e);
            if (ClientRectangle.Width == 0 || ClientRectangle.Height == 0) return;

            ToolWindowCaptionPalette palette = DockPane.IsActivePane
                ? DockPane.DockPanel.Theme.ColorPalette.ToolWindowCaptionActive
                : DockPane.DockPanel.Theme.ColorPalette.ToolWindowCaptionInactive;
            Rectangle caption = ClientRectangle;
            e.Graphics.FillRectangle(DockPane.DockPanel.Theme.PaintingService.GetBrush(palette.Background), caption);

            Color border = DockPane.DockPanel.Theme.ColorPalette.ToolWindowBorder;
            e.Graphics.DrawRectangle(DockPane.DockPanel.Theme.PaintingService.GetPen(border),
                caption.X, caption.Y, caption.Width - 1, caption.Height - 1);

            Rectangle text = caption;
            text.X += TextGapLeft;
            text.Width -= TextGapLeft + TextGapRight + ButtonGapRight + CloseButton.Width;
            if (ShouldShowAutoHideButton) text.Width -= AutoHideButton.Width + ButtonGapBetween;
            if (HasTabPageContextMenu) text.Width -= OptionsButton.Width + ButtonGapBetween;
            text.Y += TextGapTop;
            text.Height -= TextGapTop + TextGapBottom;

            TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;
            if (RightToLeft != RightToLeft.No) flags |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
            TextRenderer.DrawText(e.Graphics, DockPane.CaptionText, TextFont, DrawHelper.RtlTransform(this, text),
                palette.Text, flags);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutButtons();
        }

        protected override void OnRefreshChanges()
        {
            if (LeftSidebarDockingPolicy.UsesHiddenCaption(DockPane.DockState))
            {
                foreach (Control control in Controls)
                    control.Visible = false;
                return;
            }

            CloseButton.Enabled = DockPane.ActiveContent?.DockHandler.CloseButton == true;
            CloseButton.Visible = DockPane.ActiveContent?.DockHandler.CloseButtonVisible == true;
            AutoHideButton.Visible = ShouldShowAutoHideButton;
            OptionsButton.Visible = HasTabPageContextMenu;
            CloseButton.RefreshChanges();
            AutoHideButton.RefreshChanges();
            OptionsButton.RefreshChanges();
            LayoutButtons();
            Invalidate();
        }

        private void LayoutButtons()
        {
            Rectangle caption = ClientRectangle;
            int x = caption.Right - ButtonGapRight - ButtonImageSize;
            int y = caption.Top + ButtonGapTop;
            Point point = new(x, y);
            CloseButton.Bounds = DrawHelper.RtlTransform(this, new Rectangle(point, new Size(ButtonImageSize, ButtonImageSize)));
            if (CloseButton.Visible) point.Offset(-(ButtonImageSize + ButtonGapBetween), 0);
            AutoHideButton.Bounds = DrawHelper.RtlTransform(this, new Rectangle(point, new Size(ButtonImageSize, ButtonImageSize)));
            if (AutoHideButton.Visible) point.Offset(-(ButtonImageSize + ButtonGapBetween), 0);
            OptionsButton.Bounds = DrawHelper.RtlTransform(this, new Rectangle(point, new Size(ButtonImageSize, ButtonImageSize)));
        }

        private bool ShouldShowAutoHideButton => !DockPane.IsFloat;

        private void Close_Click(object? sender, EventArgs e) => DockPane.CloseActiveContent();

        private void AutoHide_Click(object? sender, EventArgs e)
        {
            DockPane.DockState = DockHelper.ToggleAutoHideState(DockPane.DockState);
            if (DockHelper.IsDockStateAutoHide(DockPane.DockState))
            {
                DockPane.DockPanel.ActiveAutoHideContent = null;
                DockPane.NestedDockingStatus.NestedPanes.SwitchPaneWithFirstChild(DockPane);
            }
        }

        private void Options_Click(object? sender, EventArgs e) => ShowTabPageContextMenu(PointToClient(Control.MousePosition));

        protected override bool CanDragAutoHide => true;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UiScaleManager.Instance.Changed -= UiScaleChanged;
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        private void DockPaneOnDockStateChanged(object? sender, EventArgs e)
        {
            DockPane.DockPanel.PerformLayout();
            DockPane.DockPanel.Invalidate();
        }

        private void UiScaleChanged(object? sender, EventArgs e)
        {
            (_closeButton as LargeCaptionButton)?.RefreshIconSize();
            (_autoHideButton as LargeCaptionButton)?.RefreshIconSize();
            (_optionsButton as LargeCaptionButton)?.RefreshIconSize();
            PerformLayout();
            Invalidate();
        }

        private sealed class LargeCaptionButton : InertButtonBase
        {
            private readonly LargeDockPaneCaption _caption;
            private readonly Bitmap _sourceHovered;
            private readonly Bitmap _sourceNormal;
            private readonly Bitmap _sourcePressed;
            private readonly Bitmap _sourceHoveredActive;
            private readonly Bitmap _sourceActive;
            private readonly Bitmap _sourceHoveredAutoHide;
            private readonly Bitmap _sourceAutoHide;
            private readonly Bitmap _sourcePressedAutoHide;
            private Bitmap _hovered = null!;
            private Bitmap _normal = null!;
            private Bitmap _pressed = null!;
            private Bitmap _hoveredActive = null!;
            private Bitmap _active = null!;
            private Bitmap _hoveredAutoHide = null!;
            private Bitmap _autoHide = null!;
            private Bitmap _pressedAutoHide = null!;

            public LargeCaptionButton(LargeDockPaneCaption caption, Bitmap hovered, Bitmap normal, Bitmap pressed,
                Bitmap hoveredActive, Bitmap active, Bitmap? hoveredAutoHide, Bitmap? autoHide, Bitmap? pressedAutoHide)
            {
                _caption = caption;
                _sourceHovered = hovered;
                _sourceNormal = normal;
                _sourcePressed = pressed;
                _sourceHoveredActive = hoveredActive;
                _sourceActive = active;
                _sourceHoveredAutoHide = hoveredAutoHide ?? hoveredActive;
                _sourceAutoHide = autoHide ?? active;
                _sourcePressedAutoHide = pressedAutoHide ?? pressed;
                RefreshIconSize();
            }

            private bool IsAutoHide => _caption.DockPane.IsAutoHide;
            private bool IsActive => _caption.DockPane.IsActivePane;
            public override Bitmap Image => IsActive ? (IsAutoHide ? _autoHide : _active) : _normal;
            public override Bitmap HoverImage => IsActive ? (IsAutoHide ? _hoveredAutoHide : _hoveredActive) : _hovered;
            public override Bitmap PressImage => IsAutoHide ? _pressedAutoHide : _pressed;

            public void RefreshIconSize()
            {
                _hovered?.Dispose();
                _normal?.Dispose();
                _pressed?.Dispose();
                _hoveredActive?.Dispose();
                _active?.Dispose();
                _hoveredAutoHide?.Dispose();
                _autoHide?.Dispose();
                _pressedAutoHide?.Dispose();
                _hovered = IconService.Resize(_sourceHovered, ButtonImageSize);
                _normal = IconService.Resize(_sourceNormal, ButtonImageSize);
                _pressed = IconService.Resize(_sourcePressed, ButtonImageSize);
                _hoveredActive = IconService.Resize(_sourceHoveredActive, ButtonImageSize);
                _active = IconService.Resize(_sourceActive, ButtonImageSize);
                _hoveredAutoHide = IconService.Resize(_sourceHoveredAutoHide, ButtonImageSize);
                _autoHide = IconService.Resize(_sourceAutoHide, ButtonImageSize);
                _pressedAutoHide = IconService.Resize(_sourcePressedAutoHide, ButtonImageSize);
                RefreshChanges();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _hovered?.Dispose();
                    _normal?.Dispose();
                    _pressed?.Dispose();
                    _hoveredActive?.Dispose();
                    _active?.Dispose();
                    _hoveredAutoHide?.Dispose();
                    _autoHide?.Dispose();
                    _pressedAutoHide?.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
