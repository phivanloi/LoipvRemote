using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace LoipvRemote.UI.DesignSystem
{
    [SupportedOSPlatform("windows")]
    public static class IconService
    {
        private sealed class OriginalImage(Image image)
        {
            public Image Image { get; } = image;
            public Image? Generated { get; set; }
        }

        private static readonly ConditionalWeakTable<ToolStripItem, OriginalImage> OriginalImages = new();

        public static Bitmap Resize(Image source, int size, bool cropTransparentPadding = true)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

            using Bitmap sourceBitmap = new(source);
            Rectangle sourceBounds = cropTransparentPadding ? FindVisibleBounds(sourceBitmap) : new Rectangle(Point.Empty, sourceBitmap.Size);
            Bitmap result = new(size, size, PixelFormat.Format32bppPArgb);
            using Graphics graphics = Graphics.FromImage(result);
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float scale = Math.Min((size - 2f) / sourceBounds.Width, (size - 2f) / sourceBounds.Height);
            int width = Math.Max(1, (int)Math.Round(sourceBounds.Width * scale));
            int height = Math.Max(1, (int)Math.Round(sourceBounds.Height * scale));
            Rectangle target = new((size - width) / 2, (size - height) / 2, width, height);
            graphics.DrawImage(sourceBitmap, target, sourceBounds, GraphicsUnit.Pixel);
            return result;
        }

        public static void ApplyToToolStrip(ToolStrip toolStrip, int size)
        {
            ArgumentNullException.ThrowIfNull(toolStrip);
            toolStrip.ImageScalingSize = new Size(size, size);
            ApplyToItems(toolStrip.Items, size);
        }

        private static void ApplyToItems(ToolStripItemCollection items, int size)
        {
            foreach (ToolStripItem item in items)
            {
                if (item.Image is Image image)
                {
                    OriginalImage state = OriginalImages.GetValue(item, _ => new OriginalImage(image));
                    state.Generated?.Dispose();
                    state.Generated = Resize(state.Image, size);
                    item.Image = state.Generated;
                    item.ImageScaling = ToolStripItemImageScaling.None;
                }

                if (item is ToolStripDropDownItem dropDown)
                    ApplyToItems(dropDown.DropDownItems, size);
            }
        }

        internal static Rectangle FindVisibleBounds(Bitmap bitmap)
        {
            int left = bitmap.Width;
            int top = bitmap.Height;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).A <= 8) continue;
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }

            return right < left
                ? new Rectangle(0, 0, bitmap.Width, bitmap.Height)
                : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }
    }
}
