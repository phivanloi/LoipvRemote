using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.App.Info;

namespace LoipvRemote.UI.Forms
{
    [SupportedOSPlatform("windows")]
    /// <summary>Startup splash implemented entirely with WinForms.</summary>
    public sealed class FrmSplashScreen : Form
    {
        private static FrmSplashScreen? instance;
        private readonly Panel content;
        private readonly TableLayoutPanel branding;
        private readonly PictureBox icon;
        private readonly Label title;
        private readonly Label subtitle;
        private readonly Label lblVersion;

        public FrmSplashScreen()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(600, 190);
            BackColor = Color.FromArgb(248, 250, 252);
            Padding = new Padding(1);

            Panel border = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(203, 213, 225),
                Padding = new Padding(1)
            };
            content = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            branding = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(24, 18, 24, 18),
                Margin = Padding.Empty
            };
            branding.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            branding.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            branding.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            icon = new()
            {
                Image = LoadSplashIcon(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(76, 76),
                Anchor = AnchorStyles.None,
                Margin = Padding.Empty
            };
            title = new()
            {
                AutoSize = true,
                Text = "LoipvRemote",
                Font = new Font("Segoe UI", 30F, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 0, 2)
            };
            subtitle = new()
            {
                AutoSize = true,
                Text = "Remote Connection Manager",
                Font = new Font("Segoe UI", 16F),
                ForeColor = Color.FromArgb(55, 65, 81),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 0, 2)
            };
            lblVersion = new Label
            {
                AutoSize = true,
                Text = $@"Phiên bản {GeneralAppInfo.ApplicationVersion}",
                Font = new Font("Segoe UI", 14F),
                ForeColor = Color.FromArgb(100, 116, 139),
                Anchor = AnchorStyles.Left,
                Margin = Padding.Empty
            };
            FlowLayoutPanel textStack = new()
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(24, 0, 0, 0),
                Padding = Padding.Empty
            };
            textStack.Controls.Add(title);
            textStack.Controls.Add(subtitle);
            textStack.Controls.Add(lblVersion);
            branding.Controls.Add(icon, 0, 0);
            branding.Controls.Add(textStack, 1, 0);
            content.Controls.Add(branding);
            border.Controls.Add(content);
            Controls.Add(border);
            FormClosed += (_, _) => instance = null;
        }

        public static FrmSplashScreen GetInstance()
        {
            instance ??= new FrmSplashScreen();
            return instance;
        }

        private static Bitmap? LoadSplashIcon()
        {
            const string resourceName = "LoipvRemote.Resources.LoipvRemote_Icon_new.png";
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream is null)
                return null;

            using Image image = Image.FromStream(stream);
            return new Bitmap(image);
        }

    }
}
