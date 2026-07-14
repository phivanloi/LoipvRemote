using LoipvRemote.Messages;
using LoipvRemote.Properties;
using LoipvRemote.Resources.Language;
using LoipvRemote.Security.SymmetricEncryption;
using LoipvRemote.Tools;
using LoipvRemote.Tree.Root;
using LoipvRemote.UI;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Tabs;
using LoipvRemote.Protocols.Rdp;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.Infrastructure.Windows.Com;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Timers;
using System.Windows.Forms;

namespace LoipvRemote.Connection.Protocol.RDP
{
    [SupportedOSPlatform("windows")]
    public class RdpProtocol : ProtocolBase, ISupportsViewOnly
    {
        private readonly ExternalCredentialConnectorRegistry _externalCredentialConnectors;
        private readonly IStringSecretStore _userSecretStore;
        /* RDP v8 requires Windows 7 with:
         * https://support.microsoft.com/en-us/kb/2592687
         * OR
         * https://support.microsoft.com/en-us/kb/2923545
         *
         * Windows 8+ support RDP v8 out of the box.
         */

        private RdpActiveXRuntime _runtime;
        protected RdpActiveXRuntime Runtime => _runtime;
        private RdpSession? _session;
        protected virtual RdpVersion RdpProtocolVersion => global::LoipvRemote.Domain.Protocols.Rdp.RdpVersion.Rdc6;
        protected ConnectionInfo connectionInfo;
        protected Version RdpVersion;
        private readonly DisplayProperties _displayProperties;
        protected bool loginComplete;
        private bool _alertOnIdleDisconnect;
        private bool _runtimeEventsAttached;
        protected uint DesktopScaleFactor => (uint)(_displayProperties.ResolutionScalingFactor.Width * 100);
        protected readonly uint DeviceScaleFactor = 100;
        protected readonly uint Orientation = 0;


        #region Properties

        public virtual bool SmartSize
        {
            get
            {
                try
                {
                    return _runtime.SmartSize;
                }
                catch (System.Runtime.InteropServices.InvalidComObjectException)
                {
                    // The COM object is separated from its RCW, try reacquiring the RCW or recreating the COM object
                    return _runtime.SmartSize;
                }
            }
            protected set
            {
                try
                {
                    _runtime.SmartSize = value;
                }
                catch (System.Runtime.InteropServices.InvalidComObjectException)
                {
                    // The COM object is separated from its RCW, try reacquiring the RCW or recreating the COM object
                    _runtime.SmartSize = value;
                }
            }
        }

        public virtual bool Fullscreen
        {
            get => _runtime.FullScreen;
            protected set => _runtime.FullScreen = value;
        }

        public bool LoadBalanceInfoUseUtf8 { get; set; }

        public bool ViewOnly
        {
            get => _runtime.ViewOnly;
            set => _runtime.ViewOnly = value;
        }

        #endregion

        #region Constructors

        public RdpProtocol(
            ExternalCredentialConnectorRegistry externalCredentialConnectors,
            IStringSecretStore userSecretStore)
        {
            _externalCredentialConnectors = externalCredentialConnectors
                ?? throw new ArgumentNullException(nameof(externalCredentialConnectors));
            _userSecretStore = userSecretStore ?? throw new ArgumentNullException(nameof(userSecretStore));
            _displayProperties = new DisplayProperties();
            tmrReconnect.Elapsed += tmrReconnect_Elapsed;
        }

        #endregion

        #region Public Methods

        public override bool Initialize()
        {
            connectionInfo = InterfaceControl.Info;
            MessageCollector.AddMessage(MessageClass.DebugMsg, $"Requesting RDP version: {connectionInfo.RdpVersion}. Using: {RdpProtocolVersion}");
            _runtime = new RdpActiveXRuntime(RdpProtocolVersion);
            Control = _runtime.Control;
            base.Initialize();

            try
            {
                if (!InitializeActiveXControl()) return false;

                RdpVersion = _runtime.ClientVersion;

                if (RdpVersion < Versions.RDC61) return false; // only RDP versions 6.1 and greater are supported; minimum dll version checked, MSTSCLIB is not capable

                SetRdpClientProperties();

                _session = new RdpSession(_runtime);
                return _session.Initialize(new RdpConnectionOptions(connectionInfo.Hostname, connectionInfo.Port));
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace(Language.RdpSetPropsFailed, ex);
                return false;
            }
        }

        private bool InitializeActiveXControl()
        {
            try
            {
                if (!Properties.OptionsStartupExitPage.Default.DisableRefocus)
                {
                    Control.GotFocus += RdpClient_GotFocus;
                }

                _runtime.Initialize();
                while (!Control.Created)
                {
                    Thread.Sleep(50);
                    Application.DoEvents();
                }
                Control.Anchor = AnchorStyles.None;

                return true;
            }
            catch (COMException ex)
            {
                if (ex.Message.Contains("CLASS_E_CLASSNOTAVAILABLE"))
                {
                    MessageCollector.AddMessage(MessageClass.ErrorMsg, string.Format(Language.RdpProtocolVersionNotSupported, connectionInfo.RdpVersion));
                }
                else
                {
                    MessageCollector.AddExceptionMessage(Language.RdpControlCreationFailed, ex);
                }
                Control.Dispose();
                return false;
            }
        }

        public override bool Connect()
        {
            loginComplete = false;
            SetEventHandlers();

            try
            {
                if (_session is null || !_session.Connect()) return false;
                base.Connect();

                return true;
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace(Language.ConnectionOpenFailed, ex);
            }

            return false;
        }

        public override void Disconnect()
        {
            try
            {
                _session?.Disconnect();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace(Language.RdpDisconnectFailed, ex);
                Close();
            }
        }

        public override void Close()
        {
            try
            {
                _runtime?.UnsubscribeEvents();

                if (Control != null)
                {
                    Control.GotFocus -= RdpClient_GotFocus;
                }
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace("RdpProtocol: error unsubscribing event handlers", ex);
            }

            base.Close();
        }

        public void ToggleFullscreen()
        {
            try
            {
                Fullscreen = !Fullscreen;
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace(Language.RdpToggleFullscreenFailed, ex);
            }
        }

        public void ToggleSmartSize()
        {
            try
            {
                SmartSize = !SmartSize;
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace(Language.RdpToggleSmartSizeFailed, ex);
            }
        }

        /// <summary>
        /// Toggles whether the RDP ActiveX control will capture and send input events to the remote host.
        /// The local host will continue to receive data from the remote host regardless of this setting.
        /// </summary>
        public void ToggleViewOnly()
        {
            try
            {
                ViewOnly = !ViewOnly;
            }
            catch
            {
                MessageCollector.AddMessage(MessageClass.WarningMsg, $"Could not toggle view only mode for host {connectionInfo.Hostname}");
            }
        }

        public override void Focus()
        {
            try
            {
                if (Control.ContainsFocus == false)
                {
                    Control.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace(Language.RdpFocusFailed, ex);
            }
        }

        /// <summary>
        /// Determines if this version of the RDP client
        /// is supported on this machine.
        /// </summary>
        /// <returns></returns>
        public bool RdpVersionSupported()
        {
            try
            {
                return RdpActiveXRuntime.IsSupported(RdpProtocolVersion);
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Private Methods

        protected static class Versions
        {
            // https://en.wikipedia.org/wiki/Remote_Desktop_Protocol
            public static readonly Version RDC60 = new(6, 0, 6000);
            public static readonly Version RDC61 = new(6, 0, 6001);
            public static readonly Version RDC70 = new(6, 1, 7600);
            public static readonly Version RDC80 = new(6, 2, 9200);
            public static readonly Version RDC81 = new(6, 3, 9600);
            public static readonly Version RDC100 = new(10, 0, 0);
        }

        private void SetRdpClientProperties()
        {
            SetCredentials();
            SetResolution();
            _alertOnIdleDisconnect = connectionInfo.RDPAlertIdleTimeout;
            _runtime.ApplyConfiguration(new RdpRuntimeConfiguration
            {
                Server = connectionInfo.Hostname,
                FullScreenTitle = connectionInfo.Name,
                IdleTimeoutMinutes = connectionInfo.RDPMinutesToIdleTimeout,
                StartProgram = connectionInfo.RDPStartProgram,
                WorkingDirectory = connectionInfo.RDPStartProgramWorkDir,
                MaxReconnectAttempts = Settings.Default.RdpReconnectionCount,
                OverallConnectionTimeout = Settings.Default.ConRDPOverallConnectionTimeout,
                CacheBitmaps = connectionInfo.CacheBitmaps,
                EnableCredSsp = connectionInfo.UseCredSsp,
                ConnectToAdministerServer = ResolveUseConsoleSession(),
                Port = connectionInfo.Port,
                RedirectKeys = connectionInfo.RedirectKeys,
                RedirectPorts = connectionInfo.RedirectPorts,
                RedirectPrinters = connectionInfo.RedirectPrinters,
                RedirectSmartCards = connectionInfo.RedirectSmartCards,
                RedirectClipboard = connectionInfo.RedirectClipboard,
                AudioRedirectionMode = (int)connectionInfo.RedirectSound,
                DriveRedirection = connectionInfo.RedirectDiskDrives switch
                {
                    RDPDiskDrives.None => RdpDriveRedirection.None,
                    RDPDiskDrives.All => RdpDriveRedirection.All,
                    RDPDiskDrives.Custom => RdpDriveRedirection.Custom,
                    _ => RdpDriveRedirection.Local
                },
                CustomDrives = connectionInfo.RedirectDiskDrivesCustom,
                AuthenticationLevel = (uint)connectionInfo.RDPAuthenticationLevel,
                LoadBalanceInfo = string.IsNullOrEmpty(connectionInfo.LoadBalanceInfo)
                    ? string.Empty
                    : LoadBalanceInfoUseUtf8
                        ? new AzureLoadBalanceInfoEncoder().Encode(connectionInfo.LoadBalanceInfo)
                        : connectionInfo.LoadBalanceInfo,
                ColorDepth = (int)connectionInfo.Colors,
                PerformanceFlags = CalculatePerformanceFlags(),
                ConnectingText = Language.Connecting,
                ViewOnly = Force.HasFlag(ConnectionInfo.Force.ViewOnly)
            });
            SetRdGateway();
        }

        protected object GetExtendedProperty(string property)
        {
            try
            {
                // ReSharper disable once UseIndexedProperty
                return _runtime.GetExtendedProperty(property);
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage($"Error getting extended RDP property '{property}'", ex, MessageClass.WarningMsg, false);
                return null;
            }
        }

        protected void SetExtendedProperty(string property, object value)
        {
            try
            {
                // ReSharper disable once UseIndexedProperty
                _runtime.SetExtendedProperty(property, value);
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage($"Error setting extended RDP property '{property}'", ex, MessageClass.WarningMsg, false);
            }
        }

        private void SetRdGateway()
        {
            try
            {
                if (!_runtime.GatewaySupported)
                {
                    MessageCollector.AddMessage(MessageClass.InformationMsg, Language.RdpGatewayNotSupported, true);
                    return;
                }

                MessageCollector.AddMessage(MessageClass.InformationMsg, Language.RdpGatewayIsSupported, true);

                if (connectionInfo.RDGatewayUsageMethod == RDGatewayUsageMethod.Never) return;

                // USE GATEWAY
                _runtime.ConfigureGateway(
                    (uint)connectionInfo.RDGatewayUsageMethod,
                    connectionInfo.RDGatewayHostname,
                    connectionInfo.RDGatewayUseConnectionCredentials == RDGatewayUseConnectionCredentials.SmartCard);

                if (RdpVersion < Versions.RDC61 || Force.HasFlag(ConnectionInfo.Force.NoCredentials)) return;

                switch (connectionInfo.RDGatewayUseConnectionCredentials)
                {
                    case RDGatewayUseConnectionCredentials.Yes:
                        _runtime.SetGatewayCredentials(
                            connectionInfo.Username,
                            connectionInfo.Password,
                            connectionInfo.Domain);
                        break;
                    case RDGatewayUseConnectionCredentials.SmartCard:
                        _runtime.DisableGatewayCredentialSharing();
                        break;
                    default:
                    {
                        _runtime.DisableGatewayCredentialSharing();

                            string gwu = connectionInfo.RDGatewayUsername;
                            string gwp = connectionInfo.RDGatewayPassword;
                            string gwd = connectionInfo.RDGatewayDomain;
                            string pkey = "";

                        ExternalCredentialProvider gatewayProvider = InterfaceControl.Info.RDGatewayExternalCredentialProvider;
                        if (gatewayProvider is ExternalCredentialProvider.DelineaSecretServer
                            or ExternalCredentialProvider.ClickstudiosPasswordState
                            or ExternalCredentialProvider.OnePassword)
                        {
                            try
                            {
                                string RDGUserViaAPI = InterfaceControl.Info.RDGatewayUserViaAPI;
                                var credential = _externalCredentialConnectors.GetRequired(gatewayProvider.ToString()).Resolve(RDGUserViaAPI);
                                gwu = credential.Username;
                                gwp = credential.Password;
                                gwd = credential.Domain;
                                pkey = credential.PrivateKey;
                            }
                            catch (Exception ex)
                            {
                                Event_ErrorOccured(this, $"{gatewayProvider} credential connector error: {ex.Message}", 0);
                            }
                        }
                        else if (InterfaceControl.Info.ExternalCredentialProvider == ExternalCredentialProvider.VaultOpenbao)
                        {
                            try {
                                var credential = _externalCredentialConnectors.Resolve(
                                    ExternalCredentialProvider.VaultOpenbao.ToString(),
                                    new("", gwu, connectionInfo.Hostname, connectionInfo.VaultOpenbaoMount, connectionInfo.VaultOpenbaoRole,
                                        (int)connectionInfo.VaultOpenbaoSecretEngine,
                                        LoipvRemote.Connectors.Abstractions.ExternalCredentialProtocol.Rdp));
                                gwu = credential.Username;
                                gwp = credential.Password;
                            } catch (Exception ex) {
                                Event_ErrorOccured(this, "Secret Server Interface Error: " + ex.Message, 0);
                            }
                        }


                            if (connectionInfo.RDGatewayUseConnectionCredentials != RDGatewayUseConnectionCredentials.AccessToken)
                        {
                            _runtime.SetGatewayCredentials(gwu, gwp, gwd);
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace(Language.RdpSetGatewayFailed, ex);
            }
        }

        private bool ResolveUseConsoleSession()
        {
            if (Force.HasFlag(ConnectionInfo.Force.UseConsoleSession))
                return true;
            if (Force.HasFlag(ConnectionInfo.Force.DontUseConsoleSession))
                return false;
            return connectionInfo.UseConsoleSession;
        }

        private void SetCredentials()
        {
            try
            {
                if (Force.HasFlag(ConnectionInfo.Force.NoCredentials))
                {
                    return;
                }

                string userName = connectionInfo?.Username ?? "";
                string domain = connectionInfo?.Domain ?? "";
                string userViaApi = connectionInfo?.UserViaAPI ?? "";
                string pkey = "";
                //string password = (connectionInfo?.Password?.ConvertToUnsecureString() ?? "");
                string password = (connectionInfo?.Password ?? "");

                ExternalCredentialProvider provider = InterfaceControl.Info.ExternalCredentialProvider;
                if (provider is ExternalCredentialProvider.DelineaSecretServer
                    or ExternalCredentialProvider.ClickstudiosPasswordState
                    or ExternalCredentialProvider.OnePassword)
                {
                    try
                    {
                        var credential = _externalCredentialConnectors.GetRequired(provider.ToString()).Resolve(userViaApi);
                        userName = credential.Username;
                        password = credential.Password;
                        domain = credential.Domain;
                        pkey = credential.PrivateKey;
                    }
                    catch (Exception ex)
                    {
                        Event_ErrorOccured(this, $"{provider} credential connector error: {ex.Message}", 0);
                    }
                }
                else if (InterfaceControl.Info.ExternalCredentialProvider == ExternalCredentialProvider.VaultOpenbao) {
                    try {
                        var credential = _externalCredentialConnectors.Resolve(
                            ExternalCredentialProvider.VaultOpenbao.ToString(),
                            new("", userName, connectionInfo?.Hostname ?? "", connectionInfo?.VaultOpenbaoMount ?? "", connectionInfo?.VaultOpenbaoRole ?? "",
                                (int)connectionInfo.VaultOpenbaoSecretEngine,
                                LoipvRemote.Connectors.Abstractions.ExternalCredentialProtocol.Rdp));
                        userName = credential.Username;
                        password = credential.Password;
                    } catch (Exception ex) {
                        Event_ErrorOccured(this, "Secret Server Interface Error: " + ex.Message, 0);
                    }
                }

                if (string.IsNullOrEmpty(userName))
                {
                    switch (Properties.OptionsCredentialsPage.Default.EmptyCredentials)
                    {
                        case "windows":
                            userName = Environment.UserName;
                            break;
                        case "custom" when !string.IsNullOrEmpty(Properties.OptionsCredentialsPage.Default.DefaultUsername):
                            userName = Properties.OptionsCredentialsPage.Default.DefaultUsername;
                            break;
                        case "custom":
                            try
                            {
                                var credential = _externalCredentialConnectors
                                    .GetRequired(ExternalCredentialProvider.DelineaSecretServer.ToString())
                                    .Resolve(Properties.OptionsCredentialsPage.Default.UserViaAPIDefault);
                                userName = credential.Username;
                                password = credential.Password;
                                domain = credential.Domain;
                                pkey = credential.PrivateKey;
                            }
                            catch (Exception ex)
                            {
                                Event_ErrorOccured(this, "Secret Server Interface Error: " + ex.Message, 0);
                            }

                            break;
                    }
                }
                // Restricted Admin and Remote Credential Guard modes use the current user's Kerberos
                // credentials and do not forward explicit passwords to the remote host.
                // Skipping password assignment avoids potential NTLM fallback attempts that would
                // fail for accounts in the AD Protected Users security group.
                if (RdpCredentialPolicy.ShouldAssignClearTextPassword(
                        connectionInfo.UseRestrictedAdmin,
                        connectionInfo.UseRCG))
                {
                    if (string.IsNullOrEmpty(password))
                    {
                        if (Properties.OptionsCredentialsPage.Default.EmptyCredentials == "custom")
                        {
                            if (Properties.OptionsCredentialsPage.Default.DefaultPassword != "")
                            {
                                password = _userSecretStore.Unprotect(
                                    Properties.OptionsCredentialsPage.Default.DefaultPassword,
                                    SecretPurposes.DefaultCredentialPassword);
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(domain))
                {
                    domain = Properties.OptionsCredentialsPage.Default.EmptyCredentials switch
                    {
                        "windows" => Environment.UserDomainName,
                        "custom" => Properties.OptionsCredentialsPage.Default.DefaultDomain,
                        _ => string.Empty
                    };
                }

                _runtime.ApplyCredentials(new RdpCredentialConfiguration(
                    userName,
                    password,
                    domain,
                    RdpCredentialPolicy.ShouldAssignClearTextPassword(
                        connectionInfo.UseRestrictedAdmin,
                        connectionInfo.UseRCG)));
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace(Language.RdpSetCredentialsFailed, ex);
            }
        }

        private void SetResolution()
        {
            try
            {
                if (Force.HasFlag(ConnectionInfo.Force.Fullscreen))
                {
                    Size size = Screen.FromControl(MainWindow).Bounds.Size;
                    _runtime.ApplyDisplay(new RdpDisplayConfiguration(
                        size.Width, size.Height, true, false, DesktopScaleFactor, DeviceScaleFactor));

                    return;
                }

                switch (InterfaceControl.Info.Resolution)
                {
                    case RDPResolutions.FitToWindow:
                        // Lock the RDP session to the current content area size.
                        // The control is undocked so it keeps this fixed size;
                        // AutoScroll on the parent panel provides scrollbars
                        // when the panel shrinks below the session resolution.
                        // Use DisplayRectangle to respect Padding (connection frame border).
                        var fitRect = InterfaceControl.DisplayRectangle;
                        _runtime.ApplyDisplay(new RdpDisplayConfiguration(
                            fitRect.Width, fitRect.Height, false, false, DesktopScaleFactor, DeviceScaleFactor));
                        Control.Dock = DockStyle.None;
                        Control.Location = fitRect.Location;
                        Control.Size = fitRect.Size;
                        InterfaceControl.AutoScroll = true;
                        InterfaceControl.AutoScrollMinSize = fitRect.Size;
                        break;
                    case RDPResolutions.SmartSize:
                        // Connect at the full screen resolution so the remote
                        // desktop is rendered at high quality, then SmartSizing
                        // scales the image to fit whatever the panel size is.
                        // Use Anchor instead of Dock.Fill because the AxHost
                        // ActiveX wrapper doesn't forward Dock-triggered resizes
                        // to the COM control's internal rendering surface.
                        // Use DisplayRectangle to respect Padding (connection frame border).
                        var screen = Screen.FromControl(MainWindow);
                        _runtime.ApplyDisplay(new RdpDisplayConfiguration(
                            screen.Bounds.Width, screen.Bounds.Height, false, true, DesktopScaleFactor, DeviceScaleFactor));
                        var smartRect = InterfaceControl.DisplayRectangle;
                        Control.Dock = DockStyle.None;
                        Control.Location = smartRect.Location;
                        Control.Size = smartRect.Size;
                        Control.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                        break;
                    case RDPResolutions.Fullscreen:
                        Size fullscreenSize = Screen.FromControl(MainWindow).Bounds.Size;
                        _runtime.ApplyDisplay(new RdpDisplayConfiguration(
                            fullscreenSize.Width, fullscreenSize.Height, true, false, DesktopScaleFactor, DeviceScaleFactor));
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace(Language.RdpSetResolutionFailed, ex);
            }
        }

        private int CalculatePerformanceFlags()
        {
            int flags = 0;
            if (!connectionInfo.DisplayThemes)
                flags |= (int)RDPPerformanceFlags.DisableThemes;
            if (!connectionInfo.DisplayWallpaper)
                flags |= (int)RDPPerformanceFlags.DisableWallpaper;
            if (connectionInfo.EnableFontSmoothing)
                flags |= (int)RDPPerformanceFlags.EnableFontSmoothing;
            if (connectionInfo.EnableDesktopComposition)
                flags |= (int)RDPPerformanceFlags.EnableDesktopComposition;
            if (connectionInfo.DisableFullWindowDrag)
                flags |= (int)RDPPerformanceFlags.DisableFullWindowDrag;
            if (connectionInfo.DisableMenuAnimations)
                flags |= (int)RDPPerformanceFlags.DisableMenuAnimations;
            if (connectionInfo.DisableCursorShadow)
                flags |= (int)RDPPerformanceFlags.DisableCursorShadow;
            if (connectionInfo.DisableCursorBlinking)
                flags |= (int)RDPPerformanceFlags.DisableCursorBlinking;
            return flags;
        }

        protected virtual void SetEventHandlers()
        {
            try
            {
                if (!_runtimeEventsAttached)
                {
                    _runtime.Connecting += (_, _) => RDPEvent_OnConnecting();
                    _runtime.Connected += (_, _) => RDPEvent_OnConnected();
                    _runtime.LoginComplete += (_, _) => RDPEvent_OnLoginComplete();
                    _runtime.FatalError += (_, code) => RDPEvent_OnFatalError(code);
                    _runtime.Disconnected += (_, reason) => RDPEvent_OnDisconnected(reason);
                    _runtime.IdleTimeout += (_, _) => RDPEvent_OnIdleTimeoutNotification();
                    _runtime.LeaveFullScreen += (_, _) => RDPEvent_OnLeaveFullscreenMode();
                    _runtimeEventsAttached = true;
                }
                _runtime.SubscribeEvents();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionStackTrace(Language.RdpSetEventHandlersFailed, ex);
            }
        }

        #endregion

        #region Private Events & Handlers

        private void RDPEvent_OnIdleTimeoutNotification()
        {
            Close(); //Simply close the RDP Session if the idle timeout has been triggered.

            if (!_alertOnIdleDisconnect) return;
            MessageBox.Show($@"The {connectionInfo.Name} session was disconnected due to inactivity", @"Session Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RDPEvent_OnFatalError(int errorCode)
        {
            string resourceKey = RdpErrorResourceKeys.GetErrorResourceKey(errorCode);
            string errorMsg = Language.ResourceManager.GetString(resourceKey) ?? Language.RdpErrorUnknown;
            Event_ErrorOccured(this, errorMsg, errorCode);
        }

        private void RDPEvent_OnDisconnected(int discReason)
        {
            const int UI_ERR_NORMAL_DISCONNECT = 0xB08;
            if (discReason != UI_ERR_NORMAL_DISCONNECT)
            {
                string reason = _runtime.GetErrorDescription(discReason);
                Event_Disconnected(this, reason, discReason);
            }

            if (Properties.OptionsAdvancedPage.Default.ReconnectOnDisconnect)
            {
                ReconnectGroup = new ReconnectGroup();
                ReconnectGroup.CloseClicked += Event_ReconnectGroupCloseClicked;
                ReconnectGroup.Left = (int)((double)Control.Width / 2 - (double)ReconnectGroup.Width / 2);
                ReconnectGroup.Top = (int)((double)Control.Height / 2 - (double)ReconnectGroup.Height / 2);
                ReconnectGroup.Parent = Control;
                ReconnectGroup.Show();
                tmrReconnect.Enabled = true;
            }
            else
            {
                Close();
            }
        }

        private void RDPEvent_OnConnecting()
        {
            Event_Connecting(this);
        }

        private void RDPEvent_OnConnected()
        {
            Event_Connected(this);
        }

        private void RDPEvent_OnLoginComplete()
        {
            loginComplete = true;
        }

        private void RDPEvent_OnLeaveFullscreenMode()
        {
            Fullscreen = false;
            _leaveFullscreenEvent?.Invoke(this, EventArgs.Empty);
        }

        private void RdpClient_GotFocus(object sender, EventArgs e)
        {
            ((ConnectionTab)Control.Parent.Parent).Focus();
        }
        #endregion

        #region Public Events & Handlers

        public delegate void LeaveFullscreenEventHandler(object sender, EventArgs e);

        private LeaveFullscreenEventHandler _leaveFullscreenEvent;

        public event LeaveFullscreenEventHandler LeaveFullscreen
        {
            add => _leaveFullscreenEvent = (LeaveFullscreenEventHandler)Delegate.Combine(_leaveFullscreenEvent, value);
            remove => _leaveFullscreenEvent = (LeaveFullscreenEventHandler)Delegate.Remove(_leaveFullscreenEvent, value);
        }

        #endregion

        #region Enums

        public enum Defaults
        {
            Colors = RDPColors.Colors16Bit,
            Sounds = RDPSounds.DoNotPlay,
            Resolution = RDPResolutions.SmartSize,
            Port = 3389
        }

        #endregion

        #region Reconnect Stuff

        private void tmrReconnect_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                bool srvReady = PortScanner.IsPortOpen(connectionInfo.Hostname, Convert.ToString(connectionInfo.Port));

                ReconnectGroup.ServerReady = srvReady;

                if (!ReconnectGroup.ReconnectWhenReady || !srvReady) return;
                tmrReconnect.Enabled = false;
                ReconnectGroup.DisposeReconnectGroup();
                //SetProps()
                _runtime.Connect();
            }
            catch (Exception ex)
            {
                MessageCollector.AddExceptionMessage(
                    string.Format(Language.AutomaticReconnectError, connectionInfo.Hostname),
                    ex, MessageClass.WarningMsg, false);
            }
        }

        #endregion

    }
}
