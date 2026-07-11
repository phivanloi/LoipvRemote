using System;
using System.Windows.Forms;
using LoipvRemote.App;
using LoipvRemote.Properties;
using LoipvRemote.Tools;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;
using LoipvRemote.Config.Settings.Registry;
using LoipvRemote.UI.DesignSystem;
using System.Drawing;
using System.Linq;

namespace LoipvRemote.UI.Forms.OptionsPages
{
    [SupportedOSPlatform("windows")]
    public sealed partial class AppearancePage
    {
        private OptRegistryAppearancePage pageRegSettingsInstance;
        private GroupBox groupUiSizing = null!;
        private ComboBox cboUiFont = null!;
        private NumericUpDown numUiFontScale = null!;
        private NumericUpDown numUiIconSize = null!;
        private ComboBox cboUiDensity = null!;
        private Label lblUiFont = null!;
        private Label lblUiScale = null!;
        private Label lblUiIcon = null!;
        private Label lblUiDensity = null!;
        private Label lblUiPreview = null!;
        private Button btnResetUiSizing = null!;
        private Font? uiPreviewFont;

        public AppearancePage()
        {
            InitializeComponent();
            InitializeUiSizingControls();
            ApplyTheme();
            PageIcon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.Panel_16x);
        }

        public override string PageName
        {
            get => Language.Appearance;
            set { }
        }

        public override void ApplyLanguage()
        {
            base.ApplyLanguage();

            lblLanguage.Text = Language.LanguageString;
            lblLanguageRestartRequired.Text =
                string.Format(Language.LanguageRestartRequired, Application.ProductName);
            chkShowDescriptionTooltipsInTree.Text = Language.ShowDescriptionTooltips;
            chkShowFullConnectionsFilePathInTitle.Text = Language.ShowFullConsFilePath;
            chkShowSystemTrayIcon.Text = Language.AlwaysShowSysTrayIcon;
            chkMinimizeToSystemTray.Text = Language.MinimizeToSysTray;
            chkCloseToSystemTray.Text = Language.CloseToSysTray;
            lblRegistrySettingsUsedInfo.Text = Language.OptionsCompanyPolicyMessage;

            bool vietnamese = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
            groupUiSizing.Text = vietnamese ? "Kích thước giao diện" : "Interface sizing";
            lblUiFont.Text = vietnamese ? "Phông chữ" : "Font";
            lblUiScale.Text = vietnamese ? "Cỡ chữ" : "Text size";
            lblUiIcon.Text = vietnamese ? "Cỡ biểu tượng" : "Icon size";
            lblUiDensity.Text = vietnamese ? "Mật độ" : "Density";
            btnResetUiSizing.Text = vietnamese ? "Khôi phục mặc định" : "Reset defaults";
            UpdateUiPreview();
        }

        public override void LoadSettings()
        {
            cboLanguage.Items.Clear();
            cboLanguage.Items.Add(Language.LanguageDefault);

            foreach (string nativeName in SupportedCultures.CultureNativeNames)
            {
                cboLanguage.Items.Add(nativeName);
            }

            if (!string.IsNullOrEmpty(Settings.Default.OverrideUICulture) &&
                SupportedCultures.IsNameSupported(Settings.Default.OverrideUICulture))
            {
                cboLanguage.SelectedItem = SupportedCultures.get_CultureNativeName(Settings.Default.OverrideUICulture);
            }

            if (cboLanguage.SelectedIndex == -1)
            {
                cboLanguage.SelectedIndex = 0;
            }

            chkShowDescriptionTooltipsInTree.Checked = Properties.OptionsAppearancePage.Default.ShowDescriptionTooltipsInTree;
            chkShowFullConnectionsFilePathInTitle.Checked = Properties.OptionsAppearancePage.Default.ShowCompleteConsPathInTitle;
            chkShowSystemTrayIcon.Checked = Properties.OptionsAppearancePage.Default.ShowSystemTrayIcon;
            chkMinimizeToSystemTray.Checked = Properties.OptionsAppearancePage.Default.MinimizeToTray;
            chkCloseToSystemTray.Checked = Properties.OptionsAppearancePage.Default.CloseToTray;
            cboUiFont.SelectedItem = Properties.OptionsAppearancePage.Default.UiFontFamily;
            if (cboUiFont.SelectedIndex < 0) cboUiFont.SelectedIndex = 0;
            numUiFontScale.Value = Math.Clamp(Properties.OptionsAppearancePage.Default.UiFontScalePercent, 90, 150);
            numUiIconSize.Value = Math.Clamp(Properties.OptionsAppearancePage.Default.UiIconSize, 16, 28);
            cboUiDensity.SelectedItem = Properties.OptionsAppearancePage.Default.UiDensity;
            if (cboUiDensity.SelectedIndex < 0) cboUiDensity.SelectedItem = UiDensity.Standard.ToString();
            UpdateUiPreview();
        }

        public override void SaveSettings()
        {
            if (cboLanguage.SelectedIndex > 0 &&
                SupportedCultures.IsNativeNameSupported(Convert.ToString(cboLanguage.SelectedItem)))
            {
                Settings.Default.OverrideUICulture = SupportedCultures.get_CultureName(Convert.ToString(cboLanguage.SelectedItem));
            }
            else
            {
                Settings.Default.OverrideUICulture = string.Empty;
            }

            Properties.OptionsAppearancePage.Default.ShowDescriptionTooltipsInTree = chkShowDescriptionTooltipsInTree.Checked;
            Properties.OptionsAppearancePage.Default.ShowCompleteConsPathInTitle = chkShowFullConnectionsFilePathInTitle.Checked;
            FrmMain.Default.ShowFullPathInTitle = chkShowFullConnectionsFilePathInTitle.Checked;

            Properties.OptionsAppearancePage.Default.ShowSystemTrayIcon = chkShowSystemTrayIcon.Checked;
            if (Properties.OptionsAppearancePage.Default.ShowSystemTrayIcon)
            {
                if (Runtime.NotificationAreaIcon == null)
                {
                    Runtime.NotificationAreaIcon = new NotificationAreaIcon();
                }
            }
            else
            {
                if (Runtime.NotificationAreaIcon != null)
                {
                    Runtime.NotificationAreaIcon.Dispose();
                    Runtime.NotificationAreaIcon = null;
                }
            }

            Properties.OptionsAppearancePage.Default.MinimizeToTray = chkMinimizeToSystemTray.Checked;
            Properties.OptionsAppearancePage.Default.CloseToTray = chkCloseToSystemTray.Checked;

            Properties.OptionsAppearancePage.Default.UiFontFamily = Convert.ToString(cboUiFont.SelectedItem) ?? "System";
            Properties.OptionsAppearancePage.Default.UiFontScalePercent = Decimal.ToInt32(numUiFontScale.Value);
            Properties.OptionsAppearancePage.Default.UiIconSize = Decimal.ToInt32(numUiIconSize.Value);
            Properties.OptionsAppearancePage.Default.UiDensity = Convert.ToString(cboUiDensity.SelectedItem) ?? UiDensity.Standard.ToString();
            Properties.OptionsAppearancePage.Default.Save();
            UiScaleManager.Instance.RefreshFromSettings();
        }

        private void InitializeUiSizingControls()
        {
            AutoScroll = true;

            groupUiSizing = new GroupBox
            {
                Name = "groupUiSizing",
                Text = "Kích thước giao diện",
                Location = new Point(3, 250),
                Size = new Size(590, 195),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblUiFont = NewLabel("Phông chữ", 16, 31);
            cboUiFont = new ComboBox
            {
                Name = "cboUiFont",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(140, 27),
                Size = new Size(190, 28)
            };
            cboUiFont.Items.AddRange(["System", "Segoe UI Variable Text", "Segoe UI"]);

            lblUiScale = NewLabel("Cỡ chữ", 16, 72);
            numUiFontScale = NewNumber("numUiFontScale", 140, 68, 90, 150, 5);
            Label lblScaleUnit = NewLabel("%", 217, 72);

            lblUiIcon = NewLabel("Cỡ biểu tượng", 300, 72);
            numUiIconSize = NewNumber("numUiIconSize", 420, 68, 16, 28, 2);
            numUiIconSize.Width = 80;
            Label lblIconUnit = NewLabel("px", 507, 72);

            lblUiDensity = NewLabel("Mật độ", 16, 113);
            cboUiDensity = new ComboBox
            {
                Name = "cboUiDensity",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(140, 109),
                Size = new Size(190, 28)
            };
            cboUiDensity.Items.AddRange(Enum.GetNames<UiDensity>());

            btnResetUiSizing = new Button
            {
                Name = "btnResetUiSizing",
                Text = "Khôi phục mặc định",
                Location = new Point(350, 107),
                Size = new Size(210, 32)
            };

            lblUiPreview = new Label
            {
                Name = "lblUiPreview",
                AutoSize = false,
                Location = new Point(16, 150),
                Size = new Size(544, 28),
                TextAlign = ContentAlignment.MiddleLeft
            };

            groupUiSizing.Controls.AddRange([lblUiFont, cboUiFont, lblUiScale, numUiFontScale, lblScaleUnit,
                lblUiIcon, numUiIconSize, lblIconUnit, lblUiDensity, cboUiDensity, btnResetUiSizing, lblUiPreview]);
            pnlOptions.Controls.Add(groupUiSizing);
            PositionUiSizingGroup();
            pnlOptions.Layout += (_, _) => PositionUiSizingGroup();
            Disposed += (_, _) => uiPreviewFont?.Dispose();

            numUiFontScale.ValueChanged += (_, _) => UpdateUiPreview();
            numUiIconSize.ValueChanged += (_, _) => UpdateUiPreview();
            cboUiFont.SelectedIndexChanged += (_, _) => UpdateUiPreview();
            cboUiDensity.SelectedIndexChanged += (_, _) => UpdateUiPreview();
            btnResetUiSizing.Click += (_, _) =>
            {
                cboUiFont.SelectedItem = "System";
                numUiFontScale.Value = UiPreferences.DefaultFontScalePercent;
                numUiIconSize.Value = UiPreferences.DefaultIconSize;
                cboUiDensity.SelectedItem = UiDensity.Standard.ToString();
                UpdateUiPreview();
            };
        }

        private void PositionUiSizingGroup()
        {
            if (groupUiSizing == null || pnlOptions == null) return;
            int contentBottom = pnlOptions.Controls.Cast<Control>()
                .Where(control => control != groupUiSizing && control.Visible)
                .Select(control => control.Bottom)
                .DefaultIfEmpty(0)
                .Max();
            int top = contentBottom + 12;
            if (groupUiSizing.Top != top) groupUiSizing.Top = top;
            int requiredHeight = groupUiSizing.Bottom + 15;
            if (pnlOptions.Height != requiredHeight) pnlOptions.Height = requiredHeight;
        }

        private void UpdateUiPreview()
        {
            if (lblUiPreview == null || numUiFontScale == null) return;
            UiDensity density = Enum.TryParse(Convert.ToString(cboUiDensity.SelectedItem), out UiDensity parsed)
                ? parsed
                : UiDensity.Standard;
            UiPreferences preferences = new(Convert.ToString(cboUiFont.SelectedItem), Decimal.ToInt32(numUiFontScale.Value),
                Decimal.ToInt32(numUiIconSize.Value), density);
            UiMetrics metrics = new(preferences);
            string family = preferences.FontFamily == "System" ? SystemFonts.MessageBoxFont.Name : preferences.FontFamily;
            Font replacement = new(family, metrics.BodyFontPoints, FontStyle.Regular, GraphicsUnit.Point);
            Font? previous = uiPreviewFont;
            uiPreviewFont = replacement;
            lblUiPreview.Font = replacement;
            previous?.Dispose();
            lblUiPreview.Text = $"LoipvRemote — Tiếng Việt · {preferences.FontScalePercent}% · icon {preferences.IconSize}px · {density}";
        }

        private static Label NewLabel(string text, int x, int y) => new()
        {
            AutoSize = true,
            Text = text,
            Location = new Point(x, y)
        };

        private static NumericUpDown NewNumber(string name, int x, int y, int minimum, int maximum, int increment) => new()
        {
            Name = name,
            Location = new Point(x, y),
            Size = new Size(70, 28),
            Minimum = minimum,
            Maximum = maximum,
            Increment = increment
        };

        public override void LoadRegistrySettings()
        {
            Type settingsType = typeof(OptRegistryAppearancePage);
            RegistryLoader.RegistrySettings.TryGetValue(settingsType, out var settings);
            pageRegSettingsInstance = settings as OptRegistryAppearancePage;

            // If registry settings don't exist, create a default instance to prevent null reference exceptions
            if (pageRegSettingsInstance == null)
            {
                pageRegSettingsInstance = new OptRegistryAppearancePage();
                Logger.Instance.Log?.Debug("[AppearancePage.LoadRegistrySettings] pageRegSettingsInstance was null, created default instance");
            }

            RegistryLoader.Cleanup(settingsType);

            // ***
            // Disable controls based on the registry settings.
            //
            if (pageRegSettingsInstance.ShowDescriptionTooltipsInConTree.IsSet)
                DisableControl(chkShowDescriptionTooltipsInTree);

            if (pageRegSettingsInstance.ShowCompleteConFilePathInTitle.IsSet)
                DisableControl(chkShowFullConnectionsFilePathInTitle);

            if (pageRegSettingsInstance.AlwaysShowSystemTrayIcon.IsSet)
                DisableControl(chkShowSystemTrayIcon);

            if (pageRegSettingsInstance.MinimizeToTray.IsSet)
                DisableControl(chkMinimizeToSystemTray);

            if (pageRegSettingsInstance.CloseToTray.IsSet)
                DisableControl(chkCloseToSystemTray);

            // Updates the visibility of the information label indicating whether registry settings are used.
            lblRegistrySettingsUsedInfo.Visible = ShowRegistrySettingsUsedInfo();
        }

        /// <summary>
        /// Checks if specific registry settings related to appearence page are used.
        /// </summary>
        public bool ShowRegistrySettingsUsedInfo()
        {
            return pageRegSettingsInstance.ShowDescriptionTooltipsInConTree.IsSet
                || pageRegSettingsInstance.ShowCompleteConFilePathInTitle.IsSet
                || pageRegSettingsInstance.AlwaysShowSystemTrayIcon.IsSet
                || pageRegSettingsInstance.MinimizeToTray.IsSet
                || pageRegSettingsInstance.CloseToTray.IsSet;
        }
    }
}
