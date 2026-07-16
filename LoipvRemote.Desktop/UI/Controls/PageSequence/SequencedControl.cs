using System;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.Themes;

namespace LoipvRemote.UI.Controls.PageSequence
{
    [SupportedOSPlatform("windows")]
    public class SequencedControl : UserControl, ISequenceChangingNotifier
    {
        public event EventHandler? NextRequested;
        public event EventHandler? Previous;
        public event SequencedPageReplcementRequestHandler? PageReplacementRequested;

        public SequencedControl()
        {
            ThemeManager.getInstance().ThemeChanged += ApplyTheme;
            InitializeComponent();
        }

        protected virtual void RaiseNextPageEvent()
        {
            NextRequested?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void ApplyTheme()
        {
            if (!ThemeManager.getInstance().ActiveAndExtended) return;
            BackColor = ThemeManager.getInstance().ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
            ForeColor = ThemeManager.getInstance().ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");
        }

        protected virtual void RaisePreviousPageEvent()
        {
            Previous?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void RaisePageReplacementEvent(SequencedControl control, RelativePagePosition pagetoReplace)
        {
            PageReplacementRequested?.Invoke(this, new SequencedPageReplcementRequestArgs(control, pagetoReplace));
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            //
            // SequencedControl
            //
            this.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular,
                                                System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "SequencedControl";
            this.ResumeLayout(false);
        }
    }
}
