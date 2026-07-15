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
        private readonly Label lblVersion;

        public FrmSplashScreen()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(480, 144);
            BackColor = Color.FromArgb(248, 250, 252);
            Padding = new Padding(1);

            Panel border = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(203, 213, 225),
                Padding = new Padding(1)
            };
            Panel content = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            PictureBox icon = new()
            {
                Image = LoadSplashIcon(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(20, 24),
                Size = new Size(76, 76)
            };
            Label title = new()
            {
                AutoSize = true,
                Text = "LoipvRemote",
                Font = new Font("Segoe UI", 30F, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                Location = new Point(148, 26)
            };
            Label subtitle = new()
            {
                AutoSize = true,
                Text = "Remote Connection Manager",
                Font = new Font("Segoe UI", 16F),
                ForeColor = Color.FromArgb(55, 65, 81),
                Location = new Point(149, 74)
            };
            lblVersion = new Label
            {
                AutoSize = true,
                Text = $@"Phiên bản {GeneralAppInfo.ApplicationVersion}",
                Font = new Font("Segoe UI", 14F),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(149, 105)
            };
            content.Controls.AddRange([icon, title, subtitle, lblVersion]);
            border.Controls.Add(content);
            Controls.Add(border);
            FormClosed += (_, _) => instance = null;
        }

        public static FrmSplashScreen GetInstance()
        {
            instance ??= new FrmSplashScreen();
            return instance;
        }

        private static Image? LoadSplashIcon()
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
