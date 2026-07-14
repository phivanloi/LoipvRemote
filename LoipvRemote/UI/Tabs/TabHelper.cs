using LoipvRemote.UI.Window;
using System;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace LoipvRemote.UI.Tabs
{
    [SupportedOSPlatform("windows")]
    class TabHelper
    {
        private static readonly Lazy<TabHelper> lazyHelper = new(() => new TabHelper());

        public static TabHelper Instance => lazyHelper.Value;

        private TabHelper()
        {
        }

        private ConnectionTab currentTab;

        public ConnectionTab CurrentTab
        {
            get => currentTab;
            set
            {
                currentTab = value;
                findCurrentPanel();
                Trace.WriteLine("Tab got focused: " + currentTab.TabText);
            }
        }

        private void findCurrentPanel()
        {
            System.Windows.Forms.Control currentForm = currentTab.Parent;
            while (currentForm != null && !(currentForm is ConnectionWindow))
            {
                currentForm = currentForm.Parent;
            }

            if (currentForm != null)
                CurrentPanel = (ConnectionWindow)currentForm;
        }

        private ConnectionWindow currentPanel;

        public ConnectionWindow CurrentPanel
        {
            get => currentPanel;
            set
            {
                currentPanel = value;
                Trace.WriteLine("Panel got focused: " + currentPanel.TabText);
            }
        }
    }
}
