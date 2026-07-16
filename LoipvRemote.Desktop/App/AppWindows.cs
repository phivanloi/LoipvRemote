#region Usings
using System;
using System.Linq;
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
    /// <summary>
    /// Host-owned catalog for dockable desktop windows.  It deliberately has no
    /// static state so tests and a future second shell can create an isolated
    /// window graph.
    /// </summary>
    public sealed class DesktopWindowCatalog : IDisposable
    {
        private ActiveDirectoryImportWindow? _adimportForm;
        private ExternalToolsWindow? _externalappsForm;
        private PortScanWindow? _portscanForm;
        private UltraVNCWindow? _ultravncscForm;
        private ConnectionTreeWindow? _treeForm;
        private DesktopShellRuntime? _desktopShellRuntime;
        private bool _disposed;

        internal void AttachRuntime(DesktopShellRuntime desktopShellRuntime)
        {
            ArgumentNullException.ThrowIfNull(desktopShellRuntime);
            if (_desktopShellRuntime is not null && !ReferenceEquals(_desktopShellRuntime, desktopShellRuntime))
                throw new InvalidOperationException("The app-window runtime is already attached.");

            _desktopShellRuntime = desktopShellRuntime;
            if (_treeForm is not null)
                _treeForm.AttachRuntime(desktopShellRuntime);
            ConfigForm.AttachRuntime(desktopShellRuntime);
            OptionsFormWindow?.AttachRuntime(desktopShellRuntime);
            ErrorsForm.AttachServices(desktopShellRuntime.MessageCollector, desktopShellRuntime.ConnectionWorkspace, this);
            SshtransferForm.AttachServices(desktopShellRuntime.MessageCollector);
        }

        private DesktopShellRuntime DesktopShellRuntime => _desktopShellRuntime
            ?? throw new InvalidOperationException("The app-window runtime must be attached before a window is shown.");

        internal ConnectionTreeWindow TreeForm
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

        internal ConfigWindow ConfigForm { get; } = new ConfigWindow();
        internal ErrorAndInfoWindow ErrorsForm { get; } = new ErrorAndInfoWindow();
        internal SSHTransferWindow SshtransferForm { get; private set; } = new SSHTransferWindow();
        internal OptionsWindow? OptionsFormWindow { get; private set; }


        public void Show(WindowType windowType)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            try
            {
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (windowType)
                {
                    case WindowType.ActiveDirectoryImport:
                        if (_adimportForm == null || _adimportForm.IsDisposed)
                            _adimportForm = new ActiveDirectoryImportWindow(DesktopShellRuntime.ConnectionImportService, this);
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
                            DesktopShellRuntime.ConnectionImportService,
                            this);
                        DesktopShellRuntime.ConnectionWorkspace.Show(_portscanForm);
                        break;
                    case WindowType.UltraVNCSC:
                        if (_ultravncscForm == null || _ultravncscForm.IsDisposed)
                        {
                            _ultravncscForm = new UltraVNCWindow();
                            _ultravncscForm.AttachServices(DesktopShellRuntime.MessageCollector, this);
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

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            foreach (Form form in new Form?[]
            {
                _adimportForm, _externalappsForm, _portscanForm, _ultravncscForm,
                _treeForm, ConfigForm, ErrorsForm, SshtransferForm, OptionsFormWindow
            }.OfType<Form>())
            {
                if (!form.IsDisposed)
                    form.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
