using LoipvRemote.Themes;
using WeifenLuo.WinFormsUI.Docking;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.Messages;
using LoipvRemote.UI.Window;
using LoipvRemote.UI.DesignSystem;
using LoipvRemote.UI.Tabs;

namespace LoipvRemote.UI.Window
{
    [SupportedOSPlatform("windows")]
    public class BaseWindow : DockContent
    {
        #region Private Variables

        //private WindowType _WindowType;
        //private DockContent _DockPnl;
        private ThemeManager _themeManager = null!;

        #endregion

        #region Public Properties

        protected WindowType WindowType { get; set; }

        protected DockContent DockPnl { get; set; } = null!;
        #endregion

        public BaseWindow()
        {
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
        }

        #region Public Methods

        public void SetFormText(string t)
        {
            Text = t;
            TabText = t;
        }

        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
        {
            // Handle Ctrl+Tab and Ctrl+PgDn to navigate to next tab
            if (keyData == (Keys.Control | Keys.Tab) || keyData == (Keys.Control | Keys.PageDown))
            {
                if (this is ConnectionWindow connectionWindow)
                {
                    connectionWindow.NavigateToNextTab();
                    return true;
                }
            }

            // Handle Ctrl+Shift+Tab and Ctrl+PgUp to navigate to previous tab
            if (keyData == (Keys.Control | Keys.Shift | Keys.Tab) || keyData == (Keys.Control | Keys.PageUp))
            {
                if (this is ConnectionWindow connectionWindow)
                {
                    connectionWindow.NavigateToPreviousTab();
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion

        internal void ApplyTheme()
        {
            _themeManager = ThemeManager.getInstance();
            if (!_themeManager.ActiveAndExtended) return;
            BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
            ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");
        }

        protected override void OnLoad(System.EventArgs e)
        {
            base.OnLoad(e);
            if (WindowType is WindowType.Tree or WindowType.Config or WindowType.ErrorsAndInfos)
                LeftSidebarDockingPolicy.ConfigurePersistentSidebar(this);
            UiScaleManager.Instance.Apply(this);
        }


        private void InitializeComponent()
        {
            this.SuspendLayout();
            //
            // BaseWindow
            //
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "BaseWindow";
            this.ResumeLayout(false);
        }
    }
}
