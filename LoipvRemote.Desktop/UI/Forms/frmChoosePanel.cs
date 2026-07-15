using System.Windows.Forms;
using LoipvRemote.App;
using LoipvRemote.Themes;
using LoipvRemote.UI.Panels;
using LoipvRemote.UI.Window;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;

namespace LoipvRemote.UI.Forms
{
    [SupportedOSPlatform("windows")]
    public partial class FrmChoosePanel
    {
        private readonly PanelAdder _panelAdder;

        public FrmChoosePanel(PanelAdder panelAdder)
        {
            InitializeComponent();
            Icon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.Panel_16x);
            _panelAdder = panelAdder ?? throw new ArgumentNullException(nameof(panelAdder));
        }

        public string Panel
        {
            get => cbPanels.SelectedItem.ToString();
            set => cbPanels.SelectedItem = value;
        }

        private void frmChoosePanel_Load(object sender, System.EventArgs e)
        {
            ApplyLanguage();
            ApplyTheme();
            AddAvailablePanels();
        }

        private void ApplyLanguage()
        {
            btnOK.Text = Language._Ok;
            lblDescription.Text = Language.SelectPanel;
            btnNew.Text = Language._New;
            Text = Language.TitleSelectPanel;
        }

        // Apply the dark/light title bar before the window is shown to avoid a white flash.
        protected override void OnHandleCreated(System.EventArgs e)
        {
            base.OnHandleCreated(e);
            ThemeManager.getInstance().ApplyThemeToTitleBar(this);
        }

        private void ApplyTheme()
        {
            ThemeManager.getInstance().ApplyThemeToTitleBar(this);
            if (!ThemeManager.getInstance().ActiveAndExtended) return;
            BackColor = ThemeManager.getInstance().ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
            ForeColor = ThemeManager.getInstance().ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");
            lblDescription.BackColor =
                ThemeManager.getInstance().ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
            lblDescription.ForeColor =
                ThemeManager.getInstance().ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");
        }

        private void AddAvailablePanels()
        {
            cbPanels.Items.Clear();

            foreach (BaseWindow panel in _panelAdder.Panels)
            {
                cbPanels.Items.Add(panel.Text.Replace("&&", "&"));
            }

            if (cbPanels.Items.Count > 0)
            {
                cbPanels.SelectedItem = cbPanels.Items[0];
                cbPanels.Enabled = true;
                btnOK.Enabled = true;
            }
            else
            {
                cbPanels.Enabled = false;
                btnOK.Enabled = false;
            }
        }

        private void btnNew_Click(object sender, System.EventArgs e)
        {
            using (FrmInputBox frmInputBox =
                new(Language.NewPanel, Language.PanelName + ":", Language.NewPanel))
            {
                DialogResult dr = frmInputBox.ShowDialog();
                if (dr != DialogResult.OK || string.IsNullOrEmpty(frmInputBox.returnValue)) return;
                _panelAdder.AddPanel(frmInputBox.returnValue);
                AddAvailablePanels();
                cbPanels.SelectedItem = frmInputBox.returnValue;
                cbPanels.Focus();
            }
        }

        private void btnOK_Click(object sender, System.EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }
    }
}
