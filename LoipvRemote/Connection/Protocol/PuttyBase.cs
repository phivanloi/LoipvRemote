using LoipvRemote.Messages;
using LoipvRemote.Resources.Language;
using LoipvRemote.Security;
using LoipvRemote.Security.SymmetricEncryption;
using LoipvRemote.Tools;
using LoipvRemote.Tree.Root;
using LoipvRemote.UI;
using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using LoipvRemote.Infrastructure.Windows.Process;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Putty;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;

// ReSharper disable ArrangeAccessorOwnerBody

namespace LoipvRemote.Connection.Protocol
{
    [SupportedOSPlatform("windows")]
    public class PuttyBase : ProtocolBase, IEmbeddedWindow
    {
        private readonly ExternalCredentialConnectorRegistry _externalCredentialConnectors;
        private readonly IStringSecretStore _userSecretStore;
        private const int IDM_RECONF = 0x50; // PuTTY Settings Menu ID
        private const int TitleMonitorIntervalMs = 500;
        private static readonly EmbeddedWindowFocusController EmbeddedWindowInputFocusController =
            WindowsEmbeddedWindowFocusControllerFactory.Create();

        public PuttyBase(
            ExternalCredentialConnectorRegistry externalCredentialConnectors,
            IStringSecretStore userSecretStore)
        {
            _externalCredentialConnectors = externalCredentialConnectors
                ?? throw new ArgumentNullException(nameof(externalCredentialConnectors));
            _userSecretStore = userSecretStore ?? throw new ArgumentNullException(nameof(userSecretStore));
        }
        private bool _isPuttyNg;
        private bool _usesHwndParentEmbedding;
        private readonly DisplayProperties _display = new();
        private System.Threading.Timer? _titleMonitorTimer;
        private string _lastWindowTitle = string.Empty;
        private int _titleMonitorCallbackActive;
        private string? _temporaryOpeningCommandPath;
        private bool _puttyProcessStarted;
        private System.Windows.Forms.Timer? _puttyWindowTimer;
        private long _puttyWindowEmbeddingDeadline;

        #region Public Properties

        protected PuttyProtocolKind PuttyProtocol { private get; set; }

        protected PuttySshVersion PuttySSHVersion { private get; set; }

        public IntPtr PuttyHandle { get; set; }

        private readonly PuttyProcessSession _puttySession = new();

        public static string? PuttyPath { get; set; }

        public bool Focused => EmbeddedWindowOperations.IsForegroundWindow(PuttyHandle);

        public bool IsAvailable => PuttyHandle != IntPtr.Zero && isRunning();

        public override bool TryForwardInputMessage(int message, IntPtr wParam, IntPtr lParam)
        {
            if (PuttyHandle == IntPtr.Zero || !PuttyImeMessageRouter.ShouldForward(message))
                return false;

            EmbeddedWindowOperations.SendMessage(PuttyHandle, (uint)message, wParam, lParam);
            return true;
        }

        public override ProtocolCapabilities Capabilities =>
            ProtocolCapabilities.EmbeddedWindow |
            ProtocolCapabilities.Resize |
            ProtocolCapabilities.Reconnect |
            ProtocolCapabilities.CredentialInjection;

        #endregion

        #region Private Events & Handlers

        private void ProcessExited(object sender, EventArgs e)
        {
            _puttySession.Stop();
            DeleteTemporaryOpeningCommandFile();
            Event_Closed(this);
        }

        #endregion

        #region Public Methods

        public bool isRunning()
        {
            return _puttySession.IsRunning;
        }

        public override bool Connect()
        {
            string optionalTemporaryPrivateKeyPath = ""; // path to ppk file instead of password. only temporary (extracted from credential vault).
            string username = string.Empty;
            string passwordPipeName = string.Empty;
            string authenticationPluginCommand = string.Empty;
            _puttyProcessStarted = false;

            try
            {
                _isPuttyNg = PuttyTypeDetector.GetPuttyType(PuttyPath) == PuttyTypeDetector.PuttyType.PuttyNg;
                // A Win32 child window cannot be created reliably across process boundaries.
                // Keep PuTTYNG's authentication features, but embed its terminal using the
                // same hidden top-level window and SetParent flow as regular PuTTY.
                _usesHwndParentEmbedding = false;

                // Validate PuttyPath to prevent command injection
                PathValidator.ValidateExecutablePathOrThrow(PuttyPath, nameof(PuttyPath));

                if (!(InterfaceControl.Info is PuttySessionInfo))
                {
                    if (PuttyProtocol == PuttyProtocolKind.Ssh)
                    {
                        username = InterfaceControl.Info?.Username ?? "";
                        //string password = InterfaceControl.Info?.Password?.ConvertToUnsecureString() ?? "";
                        string password = InterfaceControl.Info?.Password ?? "";
                        string UserViaAPI = InterfaceControl.Info?.UserViaAPI ?? "";
                        string privatekey = "";

                        ExternalCredentialProvider provider = InterfaceControl.Info?.ExternalCredentialProvider ?? ExternalCredentialProvider.None;
                        if (provider is ExternalCredentialProvider.DelineaSecretServer
                            or ExternalCredentialProvider.ClickstudiosPasswordState
                            or ExternalCredentialProvider.OnePassword)
                        {
                            try
                            {
                                var credential = _externalCredentialConnectors.GetRequired(provider.ToString()).Resolve(UserViaAPI);
                                username = credential.Username;
                                password = credential.Password;
                                privatekey = credential.PrivateKey;

                                if (!string.IsNullOrEmpty(privatekey))
                                {
                                    optionalTemporaryPrivateKeyPath = PuttyTemporaryFileStore.WritePrivateKey(privatekey);
                                }
                            }
                            catch (Exception ex)
                            {
                                Event_ErrorOccured(this, $"{provider} credential connector error: {ex.Message}", 0);
                            }
                        }
                        else if (InterfaceControl.Info?.ExternalCredentialProvider == ExternalCredentialProvider.VaultOpenbao) {
                            try {
                                var credential = _externalCredentialConnectors.Resolve(
                                    ExternalCredentialProvider.VaultOpenbao.ToString(),
                                    new("", InterfaceControl.Info?.Username ?? "root", InterfaceControl.Info?.Hostname ?? "",
                                        InterfaceControl.Info?.VaultOpenbaoMount ?? "", InterfaceControl.Info?.VaultOpenbaoRole ?? "",
                                        (int)(InterfaceControl.Info?.VaultOpenbaoSecretEngine ?? VaultOpenbaoSecretEngine.Kv),
                                        LoipvRemote.Connectors.Abstractions.ExternalCredentialProtocol.Ssh));
                                username = credential.Username;
                                password = credential.Password;
                            } catch (Exception ex) {
                                Event_ErrorOccured(this, "Secret Server Interface Error: " + ex.Message, 0);
                            }
                        }

                        if (string.IsNullOrEmpty(username))
                        {
                            switch (Properties.OptionsCredentialsPage.Default.EmptyCredentials)
                            {
                                case "windows":
                                    username = Environment.UserName;
                                    break;
                                case "custom" when !string.IsNullOrEmpty(Properties.OptionsCredentialsPage.Default.DefaultUsername):
                                    username = Properties.OptionsCredentialsPage.Default.DefaultUsername;
                                    break;
                                case "custom":

                                    if (Properties.OptionsCredentialsPage.Default.ExternalCredentialProviderDefault == ExternalCredentialProvider.DelineaSecretServer)
                                    {
                                        try
                                        {
                                            var credential = _externalCredentialConnectors
                                                .GetRequired(ExternalCredentialProvider.DelineaSecretServer.ToString())
                                                .Resolve(Properties.OptionsCredentialsPage.Default.UserViaAPIDefault);
                                            username = credential.Username;
                                            password = credential.Password;
                                            privatekey = credential.PrivateKey;
                                        }
                                        catch (Exception ex)
                                        {
                                            Event_ErrorOccured(this, "Secret Server Interface Error: " + ex.Message, 0);
                                        }
                                    }

                                    break;
                            }
                        }


                        if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(optionalTemporaryPrivateKeyPath))
                        {
                            if (Properties.OptionsCredentialsPage.Default.EmptyCredentials == "custom")
                            {
                                password = _userSecretStore.Unprotect(
                                    Properties.OptionsCredentialsPage.Default.DefaultPassword,
                                    SecretPurposes.DefaultCredentialPassword);
                            }
                        }

                        if (!Force.HasFlag(ConnectionInfo.Force.NoCredentials))
                        {
                            if (!string.IsNullOrEmpty(password))
                            {
                                passwordPipeName = WindowsSecretPipeServer.StartPassword(
                                    "LoipvRemoteSecretPipe",
                                    password);
                            }
                        }

                        if (InterfaceControl.Info?.ExternalCredentialProvider == ExternalCredentialProvider.VaultOpenbao && InterfaceControl.Info?.VaultOpenbaoSecretEngine == VaultOpenbaoSecretEngine.SSHOTP) {
                            if (!_isPuttyNg) {
                                MessageCollector.AddMessage(MessageClass.ErrorMsg, "Cannot connect to VaultOpenbao ssh otp without using puttyng to inject authenticator plugin");
                                return false;
                            }
                            string random = string.Join("", Guid.NewGuid().ToString("n").Take(8));
                            string pipename = $"LoipvRemoteSecretPipe{random}";
                            authenticationPluginCommand = $"{App.Info.GeneralAppInfo.HomePath}\\vault-ssh-helper-plugin.exe {username} --pipeName={pipename}";
                            _ = WindowsSecretPipeServer.ServeVaultOtpWithTimeoutAsync(
                                pipename,
                                username,
                                InterfaceControl.Info.Hostname,
                                InterfaceControl.Info.Port,
                                password,
                                TimeSpan.FromSeconds(10));
                        }

                        // PuTTY reads -m only after SSH authentication completes. This avoids
                        // sending the command into an interactive username/password prompt.
                        if (!string.IsNullOrWhiteSpace(InterfaceControl.Info?.OpeningCommand))
                        {
                            _temporaryOpeningCommandPath = PuttyTemporaryFileStore.WriteOpeningCommand(InterfaceControl.Info.OpeningCommand);
                        }

                    }

                }

                string arguments = PuttyLaunchArguments.Build(new PuttyLaunchOptions
                {
                    SavedSession = InterfaceControl.Info.PuttySession,
                    UseSavedSessionOnly = InterfaceControl.Info is PuttySessionInfo,
                    Protocol = PuttyProtocol,
                    SshVersion = PuttySSHVersion,
                    Username = username,
                    PasswordPipeName = passwordPipeName,
                    PrivateKeyPath = optionalTemporaryPrivateKeyPath,
                    OpeningCommandPath = _temporaryOpeningCommandPath ?? string.Empty,
                    AuthenticationPluginCommand = authenticationPluginCommand,
                    SuppressCredentials = Force.HasFlag(ConnectionInfo.Force.NoCredentials),
                    Port = InterfaceControl.Info.Port,
                    Hostname = InterfaceControl.Info.Hostname,
                    ParentWindowHandle = _usesHwndParentEmbedding ? InterfaceControl.Handle : 0,
                    AdditionalOptions = InterfaceControl.Info.SSHOptions
                });

                if (!_puttySession.Start(new PuttyProcessStartOptions(
                        PuttyPath!,
                        arguments,
                        StartMinimized: !_usesHwndParentEmbedding), ProcessExited))
                    return false;
                _puttyProcessStarted = true;
                WindowsJobObjectProcessTracker.AddProcessHandle(_puttySession.ProcessHandle);
                StartPuttyWindowEmbedding();
                return true;
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.ConnectionFailed + Environment.NewLine + ex.Message);
                return false;
            }
            finally
            {
                // make sure to remove the private key file we created
                if (!string.IsNullOrEmpty(optionalTemporaryPrivateKeyPath))
                {
                    DeleteTemporaryPrivateKeyAfterStartup(optionalTemporaryPrivateKeyPath);
                }

                if (!_puttyProcessStarted || _puttySession.HasExited)
                    DeleteTemporaryOpeningCommandFile();
            }
        }

        private void StartPuttyWindowEmbedding()
        {
            StopPuttyWindowEmbedding();
            _puttyWindowEmbeddingDeadline = Environment.TickCount64 +
                Properties.OptionsAdvancedPage.Default.MaxPuttyWaitTime * 1000L;

            _puttyWindowTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _puttyWindowTimer.Tick += PuttyWindowTimerOnTick;
            _puttyWindowTimer.Start();
        }

        private void PuttyWindowTimerOnTick(object? sender, EventArgs e)
        {
            try
            {
                if (!TryFindPuttyWindow())
                {
                    if (_puttySession.HasExited)
                    {
                        StopPuttyWindowEmbedding();
                        ReportPuttyWindowEmbeddingFailure("PuTTY exited before its terminal window was ready.");
                    }
                    else if (Environment.TickCount64 >= _puttyWindowEmbeddingDeadline)
                    {
                        StopPuttyWindowEmbedding();
                        ReportPuttyWindowEmbeddingFailure("PuTTY terminal window was not ready before the configured timeout.");
                    }

                    return;
                }

                StopPuttyWindowEmbedding();
                EmbedPuttyWindow();
            }
            catch (Exception ex)
            {
                StopPuttyWindowEmbedding();
                ReportPuttyWindowEmbeddingFailure(ex.Message);
            }
        }

        private bool TryFindPuttyWindow()
        {
            if (PuttyHandle != IntPtr.Zero)
                return true;

            if (_puttySession.HasExited)
                return false;

            if (_usesHwndParentEmbedding)
            {
                PuttyHandle = EmbeddedWindowOperations.FindChildWindow(InterfaceControl.Handle);
                return PuttyHandle != IntPtr.Zero;
            }

            _puttySession.Refresh();
            IntPtr candidateHandle = _puttySession.MainWindowHandle;
            if (candidateHandle == IntPtr.Zero)
                return false;

            // Do not reparent host-key or authentication dialogs. They must remain
            // top-level windows so the user can see and interact with them.
            if (!EmbeddedWindowOperations.HasClassName(candidateHandle, "PuTTY"))
                return false;

            PuttyHandle = candidateHandle;
            EmbeddedWindowOperations.Hide(PuttyHandle);
            return true;
        }

        private void EmbedPuttyWindow()
        {
            if (_puttySession.HasExited || PuttyHandle == IntPtr.Zero)
                throw new InvalidOperationException("PuTTY terminal window is unavailable for embedding.");

            if (!_usesHwndParentEmbedding)
                EmbeddedWindowOperations.SetParent(PuttyHandle, InterfaceControl.Handle);

            // Apply a borderless child style after the cross-process SetParent
            // operation so only terminal content is visible.
            if (!EmbeddedWindowOperations.TrySetWindowStyle(
                    PuttyHandle,
                    PuttyEmbeddedWindowLayout.CreateBorderlessChildStyle(EmbeddedWindowOperations.GetWindowStyle(PuttyHandle))))
            {
                MessageCollector.AddMessage(MessageClass.WarningMsg,
                    Language.PuttyStuff + ": SetWindowLong returned 0, window style change may have failed", true);
            }

            EmbeddedWindowOperations.RefreshFrame(PuttyHandle);

            MessageCollector.AddMessage(MessageClass.InformationMsg, Language.PuttyStuff, true);
            MessageCollector.AddMessage(MessageClass.InformationMsg, string.Format(Language.PuttyHandle, PuttyHandle), true);
            MessageCollector.AddMessage(MessageClass.InformationMsg, string.Format(Language.PuttyTitle, _puttySession.MainWindowTitle), true);
            MessageCollector.AddMessage(MessageClass.InformationMsg, string.Format(Language.PanelHandle, InterfaceControl.Parent.Handle), true);

            if (!_usesHwndParentEmbedding)
                EmbeddedWindowOperations.Restore(PuttyHandle);

            Resize(this, EventArgs.Empty);
            base.Connect();
            StartTitleMonitor();
            FocusEmbeddedWindowWhenActive();
        }

        private void FocusEmbeddedWindowWhenActive()
        {
            if (!EmbeddedWindowActivationPolicy.ShouldRequestFocus(
                    InterfaceControl.IsDisposed,
                    InterfaceControl.IsHandleCreated,
                    InterfaceControl.Visible))
            {
                return;
            }

            InterfaceControl.BeginInvoke((MethodInvoker)(() =>
            {
                if (EmbeddedWindowActivationPolicy.ShouldRequestFocus(
                        InterfaceControl.IsDisposed,
                        InterfaceControl.IsHandleCreated,
                        InterfaceControl.Visible))
                {
                    Focus();
                }
            }));
        }

        private void StartTitleMonitor()
        {
            if (!Properties.OptionsTabsPanelsPage.Default.UseTerminalTitleForTabs || _puttySession.HasExited)
                return;

            _lastWindowTitle = _puttySession.MainWindowTitle;
            _titleMonitorTimer = new System.Threading.Timer(
                MonitorPuttyTitle,
                null,
                TitleMonitorIntervalMs,
                TitleMonitorIntervalMs);
        }

        private void StopPuttyWindowEmbedding()
        {
            if (_puttyWindowTimer == null)
                return;

            _puttyWindowTimer.Stop();
            _puttyWindowTimer.Tick -= PuttyWindowTimerOnTick;
            _puttyWindowTimer.Dispose();
            _puttyWindowTimer = null;
        }

        private void ReportPuttyWindowEmbeddingFailure(string details)
        {
            MessageCollector.AddMessage(MessageClass.ErrorMsg,
                Language.ConnectionFailed + Environment.NewLine + details);
            Event_ErrorOccured(this, details, null);
            Close();
        }

        private static void DeleteTemporaryPrivateKeyAfterStartup(string path)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(500);
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                    // PuTTY can still be opening the key; cleanup is best effort.
                }
            });
        }


        private void DeleteTemporaryOpeningCommandFile()
        {
            string? path = Interlocked.Exchange(ref _temporaryOpeningCommandPath, null);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
                // The process can still be shutting down; cleanup is best effort.
            }
        }

        public override void Focus()
        {
            try
            {
                // PuTTY is embedded as a child of the connection panel. Promoting that
                // child to the foreground changes Windows' Alt+Tab MRU ordering; setting
                // keyboard focus keeps the main LoipvRemote window as the active task.
                IntPtr ownerWindowHandle = InterfaceControl is { IsHandleCreated: true }
                    ? InterfaceControl.Handle
                    : IntPtr.Zero;
                if (!EmbeddedWindowInputFocusController.TryFocus(ownerWindowHandle, PuttyHandle))
                    return;

                EmbeddedWindowOperations.ForwardInputLanguageChange(PuttyHandle);
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.PuttyFocusFailed + Environment.NewLine + ex.Message, true);
            }
        }

        private void MonitorPuttyTitle(object? state)
        {
            if (Interlocked.Exchange(ref _titleMonitorCallbackActive, 1) != 0)
                return;

            try
            {
                if (_puttySession.HasExited)
                {
                    StopTitleMonitor();
                    return;
                }

                _puttySession.Refresh();
                string currentTitle = _puttySession.MainWindowTitle;
                if (currentTitle != _lastWindowTitle)
                {
                    _lastWindowTitle = currentTitle;
                    Event_TitleChanged(this, currentTitle);
                }
            }
            catch (Exception ex)
            {
                StopTitleMonitor();
                MessageCollector.AddMessage(MessageClass.InformationMsg,
                    "PuTTY title monitoring stopped: " + ex.Message, true);
            }
            finally
            {
                Volatile.Write(ref _titleMonitorCallbackActive, 0);
            }
        }

        private void StopTitleMonitor()
        {
            Interlocked.Exchange(ref _titleMonitorTimer, null)?.Dispose();
        }

        protected override void Resize(object sender, EventArgs e)
        {
            try
            {
                if (InterfaceControl.Size == Size.Empty)
                    return;

                // PuTTY retains a caption-sized client strip even after its
                // non-client chrome is removed. Crop that strip at the host
                // boundary so the terminal begins immediately below its tab.
                int titleStripHeight = SystemInformation.CaptionHeight + SystemInformation.FrameBorderSize.Height;
                Rectangle contentBounds = PuttyEmbeddedWindowLayout.ContentBounds(
                    InterfaceControl.RemoteContentBounds,
                    titleStripHeight);
                EmbeddedWindowOperations.Move(PuttyHandle,
                                              contentBounds.X,
                                              contentBounds.Y,
                                              contentBounds.Width,
                                              contentBounds.Height);
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.PuttyResizeFailed + Environment.NewLine + ex.Message, true);
            }
        }

        public override void Close()
        {
            EmbeddedWindowInputFocusController.Release(PuttyHandle);
            StopPuttyWindowEmbedding();
            StopTitleMonitor();
            try
            {
                _puttySession.Stop();
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.PuttyKillFailed + Environment.NewLine + ex.Message, true);
            }
            finally
            {
                DeleteTemporaryOpeningCommandFile();
            }

            base.Close();
        }

        public void ShowSettingsDialog()
        {
            try
            {
                EmbeddedWindowOperations.ShowSettingsDialog(PuttyHandle, IDM_RECONF);
            }
            catch (Exception ex)
            {
                MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.PuttyShowSettingsDialogFailed + Environment.NewLine + ex.Message, true);
            }
        }

        #endregion

    }
}
