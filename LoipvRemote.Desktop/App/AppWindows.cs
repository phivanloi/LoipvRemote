#region Usings
using System;
using System.Runtime.Versioning;
using LoipvRemote.Resources.Language;
using LoipvRemote.App.Composition;
using LoipvRemote.UI;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Window;
#endregion

namespace LoipvRemote.App
{
    [SupportedOSPlatform("windows")]
    public static class AppWindows
    {
        private static ActiveDirectoryImportWindow? _adimportForm;
        private static ExternalToolsWindow? _externalappsForm;
        private static PortScanWindow? _portscanForm;
        private static UltraVNCWindow? _ultravncscForm;
        private static ConnectionTreeWindow? _treeForm;
        private static DesktopShellRuntime? _desktopShellRuntime;

        internal static void AttachRuntime(DesktopShellRuntime desktopShellRuntime)
        {
            ArgumentNullException.ThrowIfNull(desktopShellRuntime);
            if (_desktopShellRuntime is not null && !ReferenceEquals(_desktopShellRuntime, desktopShellRuntime))
                throw new InvalidOperationException("The app-window runtime is already attached.");

            _desktopShellRuntime = desktopShellRuntime;
            if (_treeForm is not null)
                _treeForm.AttachRuntime(desktopShellRuntime);
            ConfigForm.AttachRuntime(desktopShellRuntime);
            OptionsFormWindow?.AttachRuntime(desktopShellRuntime);
            ErrorsForm.AttachServices(desktopShellRuntime.MessageCollector, desktopShellRuntime.ConnectionWorkspace);
            SshtransferForm.AttachServices(desktopShellRuntime.MessageCollector);
        }

        private static DesktopShellRuntime DesktopShellRuntime => _desktopShellRuntime
            ?? throw new InvalidOperationException("The app-window runtime must be attached before a window is shown.");

        internal static ConnectionTreeWindow TreeForm
        {
            get
            {
                _treeForm ??= new ConnectionTreeWindow();
                if (_desktopShellRuntime is not null)
                    _treeForm.AttachRuntime(_desktopShellRuntime);
                return _treeForm;
            }
            set => _treeForm = value;
        }

        internal static ConfigWindow ConfigForm { get; set; } = new ConfigWindow();
        internal static ErrorAndInfoWindow ErrorsForm { get; set; } = new ErrorAndInfoWindow();
        internal static SSHTransferWindow SshtransferForm { get; private set; } = new SSHTransferWindow();
        internal static OptionsWindow? OptionsFormWindow { get; private set; }


        public static void Show(WindowType windowType)
        {
            try
            {
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (windowType)
                {
                    case WindowType.ActiveDirectoryImport:
                        if (_adimportForm == null || _adimportForm.IsDisposed)
                            _adimportForm = new ActiveDirectoryImportWindow(DesktopShellRuntime.ConnectionImportService);
                        DesktopShellRuntime.ConnectionWorkspace.Show(_adimportForm);
                        break;
                    case WindowType.Options:
                        if (OptionsFormWindow == null || OptionsFormWindow.IsDisposed)
                            OptionsFormWindow = new OptionsWindow();
                        OptionsFormWindow.AttachRuntime(DesktopShellRuntime);
                        OptionsFormWindow.SetActivatedPage(Language.StartupExit);
                        // Reload controls from stored settings before every show so that any
                        // edits left over from a previous hide (Tab-X without Apply/OK) are
                        // discarded.  Safe on first call — no-op until FrmOptions is embedded.
                        OptionsFormWindow.RefreshSettings();
                        DesktopShellRuntime.ConnectionWorkspace.Show(OptionsFormWindow);
                        break;
                    case WindowType.SSHTransfer:
                        if (SshtransferForm == null || SshtransferForm.IsDisposed)
                        SshtransferForm = new SSHTransferWindow();
                        SshtransferForm.AttachServices(DesktopShellRuntime.MessageCollector);
                        DesktopShellRuntime.ConnectionWorkspace.Show(SshtransferForm);
                        break;
                    case WindowType.ExternalApps:
                        if (_externalappsForm == null || _externalappsForm.IsDisposed)
                            _externalappsForm = new ExternalToolsWindow(
                                DesktopShellRuntime.ExternalToolsService,
                                DesktopShellRuntime.MessageCollector);
                        DesktopShellRuntime.ConnectionWorkspace.Show(_externalappsForm);
                        break;
                    case WindowType.PortScan:
                        _portscanForm = new PortScanWindow(
                            DesktopShellRuntime.MessageCollector,
                            DesktopShellRuntime.ConnectionImportService);
                        DesktopShellRuntime.ConnectionWorkspace.Show(_portscanForm);
                        break;
                    case WindowType.UltraVNCSC:
                        if (_ultravncscForm == null || _ultravncscForm.IsDisposed)
                        {
                            _ultravncscForm = new UltraVNCWindow();
                            _ultravncscForm.AttachServices(DesktopShellRuntime.MessageCollector);
                        }
                        DesktopShellRuntime.ConnectionWorkspace.Show(_ultravncscForm);
                        break;
                }
            }
            catch (Exception ex)
            {
                DesktopShellRuntime.MessageCollector.AddExceptionStackTrace("App.Windows.Show() failed.", ex);
            }
        }
    }
}
