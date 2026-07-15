using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Tree.Root;
using LoipvRemote.UI.DesignSystem;

namespace LoipvRemote.UI
{
    [SupportedOSPlatform("windows")]
    public class StatusImageList : IDisposable
    {
        public ImageList ImageList { get; }
        private readonly UiScaleManager _uiScaleManager;
        private int _deviceDpi = 96;

        public StatusImageList()
        {
            _uiScaleManager = UiScaleManager.Instance;

            ImageList = new ImageList
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(IconPixelSize, IconPixelSize),
                TransparentColor = Color.Transparent
            };

            FillImageList(ImageList);
            _uiScaleManager.Changed += UiScaleChanged;
        }

        internal void ApplyDpi(int dpi)
        {
            int normalizedDpi = Math.Max(96, dpi);
            if (_deviceDpi == normalizedDpi)
                return;

            _deviceDpi = normalizedDpi;
            RebuildImages();
        }

        public object ImageGetter(object rowObject)
        {
            return GetKey(rowObject as ConnectionInfo);
        }

        public Image GetImage(ConnectionInfo connectionInfo)
        {
            string key = GetKey(connectionInfo);
            return ImageList.Images.ContainsKey(key)
                ? ImageList.Images[key]
                : null;
        }

        public string GetKey(ConnectionInfo connectionInfo)
        {
            if (connectionInfo == null) return "";
            if (connectionInfo is RootPuttySessionsNodeInfo) return "PuttySessions";
            if (connectionInfo is RootNodeInfo) return "Root";
            if (connectionInfo is ContainerInfo) return "Folder";

            return GetConnectionIcon(connectionInfo);
        }

        private static string BuildConnectionIconName(string icon, bool connected)
        {
            string status = connected ? "Play" : "Default";
            return $"Connection_{icon}_{status}";
        }

        private const string DefaultConnectionIcon = ConnectionIcon.LoipvRemoteIconName;

        private string GetConnectionIcon(ConnectionInfo connection)
        {
            string iconName = ConnectionIcon.GetConnectionDisplayIcon(connection.Icon);

            bool connected = connection.OpenConnections.Count > 0;
            string name = BuildConnectionIconName(iconName, connected);
            if (ImageList.Images.ContainsKey(name)) return name;
            Icon image = ConnectionIcon.FromString(iconName);
            if (image == null)
            {
                return DefaultConnectionIcon;
            }

            using Bitmap source = image.ToBitmap();
            Bitmap normal = IconService.Resize(source, IconPixelSize);
            Bitmap connectedImage = Overlay(image, Properties.Resources.ConnectedOverlay, IconPixelSize);
            ImageList.Images.Add(BuildConnectionIconName(iconName, false), normal);
            ImageList.Images.Add(BuildConnectionIconName(iconName, true), connectedImage);
            return name;
        }

        private static Bitmap Overlay(Icon background, Image foreground, int size)
        {
            using Bitmap source = background.ToBitmap();
            Bitmap result = IconService.Resize(source, size);
            using (Graphics gr = Graphics.FromImage(result))
            {
                int overlaySize = Math.Max(8, size / 2);
                gr.DrawImage(foreground, new Rectangle(size - overlaySize, size - overlaySize, overlaySize, overlaySize));
            }

            return result;
        }

        private void FillImageList(ImageList imageList)
        {
            try
            {
                int size = IconPixelSize;
                Bitmap root = IconService.Resize(Properties.Resources.ASPWebSite_16x, size);
                Bitmap folder = IconService.Resize(Properties.Resources.FolderClosed_16x, size);
                Bitmap putty = IconService.Resize(Properties.Resources.PuttySessions, size);
                imageList.Images.Add("Root", root);
                imageList.Images.Add("Folder", folder);
                imageList.Images.Add("PuttySessions", putty);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Unable to fill the image list of type {nameof(StatusImageList)}.{Environment.NewLine}{ex}");
            }
        }

        private void UiScaleChanged(object? sender, EventArgs e)
        {
            RebuildImages();
        }

        private int IconPixelSize => _uiScaleManager.Metrics.ScaleForDpi(
            _uiScaleManager.Metrics.IconSize,
            _deviceDpi / 96f);

        private void RebuildImages()
        {
            ImageList.Images.Clear();
            ImageList.ImageSize = new Size(IconPixelSize, IconPixelSize);
            FillImageList(ImageList);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _uiScaleManager.Changed -= UiScaleChanged;
                ImageList?.Dispose();
            }
        }
    }
}
