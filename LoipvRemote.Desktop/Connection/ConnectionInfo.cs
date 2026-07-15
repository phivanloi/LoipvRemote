using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using LoipvRemote.App;
using LoipvRemote.Desktop.Sessions;
using LoipvRemote.Container;
using LoipvRemote.Properties;
using LoipvRemote.Tree;
using LoipvRemote.Resources.Language;
using LoipvRemote.Tree.Root;
using System.Runtime.Versioning;

namespace LoipvRemote.Connection
{
    [SupportedOSPlatform("windows")]
    [DefaultProperty("Name")]
    public class ConnectionInfo : AbstractConnectionRecord, IHasParent, IInheritable
    {
        private ConnectionInfoInheritance _inheritance;

        #region Public Properties

        [Browsable(false)]
        public ConnectionInfoInheritance Inheritance
        {
            get => _inheritance;
            set => _inheritance = _inheritance.Parent != this
                ? _inheritance.Clone(this)
                : value;
        }

        [Browsable(false)] public ProtocolSessionCollection OpenConnections { get; protected set; } = [];

        [Browsable(false)] public virtual bool IsContainer { get; set; }

        [Browsable(false)] public bool IsDefault { get; set; }

        [Browsable(false)] public ContainerInfo Parent { get; internal set; }

        [Browsable(false)]
        public bool IsQuickConnect { get; set; }

        [Browsable(false)]
        public bool PleaseConnect { get; set; }

        #endregion

        #region Constructors

        public ConnectionInfo()
            : this(Guid.NewGuid().ToString())
        {
        }

        public ConnectionInfo(string uniqueId)
            : base(uniqueId)
        {
            _inheritance = new ConnectionInfoInheritance(this);
            Parent = null!;
            SetNewOpenConnectionList();
            SetTreeDisplayDefaults();
            SetConnectionDefaults();
            SetProtocolDefaults();
            SetRemoteDesktopServicesDefaults();
            SetRdGatewayDefaults();
            SetAppearanceDefaults();
            SetRedirectDefaults();
            SetMiscDefaults();
            SetVncDefaults();
            SetDefaults();
        }

        #endregion

        #region Public Methods

        public virtual ConnectionInfo Clone()
        {
            ConnectionInfo newConnectionInfo = new();
            newConnectionInfo.CopyFrom(this);
            return newConnectionInfo;
        }

        /// <summary>
        /// Copies all connection and inheritance values
        /// from the given <see cref="sourceConnectionInfo"/>.
        /// </summary>
        /// <param name="sourceConnectionInfo"></param>
        public void CopyFrom(ConnectionInfo sourceConnectionInfo)
        {
            IEnumerable<PropertyInfo> properties = GetType().BaseType?.GetProperties()
                .Where(prop => prop.CanRead && prop.CanWrite) ?? [];
            foreach (PropertyInfo property in properties)
            {
                if (property.Name == nameof(Parent)) continue;
                object? remotePropertyValue = property.GetValue(sourceConnectionInfo, null);
                property.SetValue(this, remotePropertyValue, null);
            }

            ConnectionInfoInheritance clonedInheritance = sourceConnectionInfo.Inheritance.Clone(this);
            Inheritance = clonedInheritance;
        }

        public virtual TreeNodeType GetTreeNodeType()
        {
            return TreeNodeType.Connection;
        }

        private void SetDefaults()
        {
            if (Port == 0)
            {
                SetDefaultPort();
            }
        }

        public int GetDefaultPort()
        {
            return GetDefaultPort(Protocol);
        }

        public void SetDefaultPort()
        {
            Port = GetDefaultPort();
        }

        protected virtual IEnumerable<PropertyInfo> GetProperties(string[] excludedPropertyNames)
        {
            PropertyInfo[] properties = typeof(ConnectionInfo).GetProperties();
            IEnumerable<PropertyInfo> filteredProperties = properties.Where((prop) => !excludedPropertyNames.Contains(prop.Name));
            return filteredProperties;
        }

        public virtual IEnumerable<PropertyInfo> GetSerializableProperties()
        {
            string[] excludedProperties = new[]
            {
                "Parent", "Name", "Hostname", "Port", "Inheritance", "OpenConnections",
                "IsContainer", "IsDefault", "PositionID", "ConstantID", "TreeNode", "IsQuickConnect", "PleaseConnect"
            };

            return GetProperties(excludedProperties);
        }

        public virtual void SetParent(ContainerInfo newParent)
        {
            RemoveParent();
            newParent?.AddChild(this);
        }

        public void RemoveParent()
        {
            Parent?.RemoveChild(this);
        }

        public ConnectionInfo GetRootParent()
        {
            return Parent != null ? Parent.GetRootParent() : this;
        }

        #endregion

        #region Public Enumerations

        [Flags()]
        public enum Force
        {
            None = 0,
            UseConsoleSession = 1,
            Fullscreen = 2,
            DoNotJump = 4,
            OverridePanel = 8,
            DontUseConsoleSession = 16,
            NoCredentials = 32,
            ViewOnly = 64
        }

        #endregion

        #region Private Methods

        protected override TPropertyType GetPropertyValue<TPropertyType>(string propertyName, TPropertyType value)
        {
            if (!ShouldThisPropertyBeInherited(propertyName))
                return value;

            bool couldGetInheritedValue =
                TryGetInheritedPropertyValue<TPropertyType>(propertyName, out TPropertyType inheritedValue);

            return couldGetInheritedValue
                ? inheritedValue
                : value;
        }

        private bool ShouldThisPropertyBeInherited(string propertyName)
        {
            return
                Inheritance.InheritanceActive &&
                ParentIsValidInheritanceTarget() &&
                IsInheritanceTurnedOnForThisProperty(propertyName);
        }

        private bool ParentIsValidInheritanceTarget()
        {
            return Parent != null;
        }

        private bool IsInheritanceTurnedOnForThisProperty(string propertyName)
        {
            Type inheritType = Inheritance.GetType();
            PropertyInfo? inheritPropertyInfo = inheritType.GetProperty(propertyName);
            bool inheritPropertyValue = inheritPropertyInfo?.GetValue(Inheritance, null) is { } rawValue &&
                                        Convert.ToBoolean(rawValue, System.Globalization.CultureInfo.InvariantCulture);
            return inheritPropertyValue;
        }

        private bool TryGetInheritedPropertyValue<TPropertyType>(string propertyName, out TPropertyType inheritedValue)
        {
            try
            {
                if (Parent is null)
                {
                    inheritedValue = default!;
                    return false;
                }

                Type connectionInfoType = Parent.GetType();
                PropertyInfo? parentPropertyInfo = connectionInfoType.GetProperty(propertyName);
                if (parentPropertyInfo == null)
                    throw new InvalidOperationException(
                        $"Could not retrieve property data for property '{propertyName}' on parent node '{Parent?.Name}'"
                    );

                if (parentPropertyInfo.GetValue(Parent, null) is TPropertyType value)
                {
                    inheritedValue = value;
                    return true;
                }

                inheritedValue = default!;
                return false;
            }
            catch (Exception e)
            {
                Trace.TraceError($"Error retrieving inherited property '{propertyName}'.{Environment.NewLine}{e}");
                inheritedValue = default!;
                return false;
            }
        }

        private static int GetDefaultPort(ProtocolKind protocol)
        {
            try
            {
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (protocol)
                {
                    case ProtocolKind.Rdp:
                        return 3389;
                    case ProtocolKind.Vnc:
                        return 5900;
                    case ProtocolKind.Ard:
                        return 5900;
                    case ProtocolKind.Ssh1:
                    case ProtocolKind.Ssh2:
                        return 22;
                    case ProtocolKind.Telnet:
                        return 23;
                    case ProtocolKind.Rlogin:
                        return 513;
                    case ProtocolKind.Raw:
                        return 23;
                    case ProtocolKind.Http:
                        return 80;
                    case ProtocolKind.Https:
                        return 443;
                    case ProtocolKind.PowerShell:
                        return 5985;
                    case ProtocolKind.Wsl:
                    case ProtocolKind.Terminal:
                        return 0;
                    case ProtocolKind.ExternalApplication:
                    case ProtocolKind.AnyDesk:
                        return 0;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"{Language.ConnectionSetDefaultPortFailed}{Environment.NewLine}{ex}");
                return 0;
            }
        }

        private void SetTreeDisplayDefaults()
        {
            Name = Language.NewConnection;
            Description = Settings.Default.ConDefaultDescription;
            Icon = Settings.Default.ConDefaultIcon;
            Panel = Language.General;
            Color = string.Empty;
            TabColor = string.Empty;
            ConnectionFrameColor = ConnectionFrameColor.None;
        }

        private void SetConnectionDefaults()
        {
            Hostname = string.Empty;
            ExternalAddressProvider = (ExternalAddressProvider)Enum.Parse(typeof(ExternalAddressProvider), Settings.Default.ConDefaultExternalAddressProvider);
            EC2Region = Settings.Default.ConDefaultEC2Region;
            ExternalCredentialProvider = (ExternalCredentialProvider)Enum.Parse(typeof(ExternalCredentialProvider), Settings.Default.ConDefaultExternalCredentialProvider);
            UserViaAPI = "";
        }

        private void SetProtocolDefaults()
        {
            Protocol = Enum.TryParse(Settings.Default.ConDefaultProtocol, ignoreCase: true, out ProtocolKind configuredProtocol)
                ? configuredProtocol
                : ProtocolKind.Rdp;
            ExtApp = Settings.Default.ConDefaultExtApp;
            Port = 0;
            PuttySession = Settings.Default.ConDefaultPuttySession;
            UseConsoleSession = Settings.Default.ConDefaultUseConsoleSession;
            RDPAuthenticationLevel = (AuthenticationLevel)Enum.Parse(typeof(AuthenticationLevel), Settings.Default.ConDefaultRDPAuthenticationLevel);
            RDPMinutesToIdleTimeout = Settings.Default.ConDefaultRDPMinutesToIdleTimeout;
            RDPAlertIdleTimeout = Settings.Default.ConDefaultRDPAlertIdleTimeout;
            LoadBalanceInfo = Settings.Default.ConDefaultLoadBalanceInfo;
            RenderingEngine = (BrowserRenderingEngine)Enum.Parse(typeof(BrowserRenderingEngine), Settings.Default.ConDefaultRenderingEngine);
            UseCredSsp = Settings.Default.ConDefaultUseCredSsp;
            UseRestrictedAdmin = Settings.Default.ConDefaultUseRestrictedAdmin;
            UseRCG = Settings.Default.ConDefaultUseRCG;
            UseRedirectionServerName = Settings.Default.ConDefaultUseRedirectionServerName;
            UseVmId = Settings.Default.ConDefaultUseVmId;
            UseEnhancedMode = Settings.Default.ConDefaultUseEnhancedMode;
        }

        private void SetRemoteDesktopServicesDefaults()
        {
            RDPStartProgram = string.Empty;
            RDPStartProgramWorkDir = string.Empty;
        }

        private void SetRdGatewayDefaults()
        {
            RDGatewayUsageMethod = (RDGatewayUsageMethod)Enum.Parse(typeof(RDGatewayUsageMethod), Settings.Default.ConDefaultRDGatewayUsageMethod);
            RDGatewayHostname = Settings.Default.ConDefaultRDGatewayHostname;
            RDGatewayUseConnectionCredentials = (RDGatewayUseConnectionCredentials)Enum.Parse(typeof(RDGatewayUseConnectionCredentials), Settings.Default.ConDefaultRDGatewayUseConnectionCredentials);
            RDGatewayUsername = Settings.Default.ConDefaultRDGatewayUsername;
            RDGatewayPassword = Settings.Default.ConDefaultRDGatewayPassword;
            RDGatewayDomain = Settings.Default.ConDefaultRDGatewayDomain;
            RDGatewayAccessToken = Settings.Default.ConDefaultRDGatewayAccessToken;
            RDGatewayExternalCredentialProvider = (ExternalCredentialProvider)Enum.Parse(typeof(ExternalCredentialProvider), Settings.Default.ConDefaultRDGatewayExternalCredentialProvider);
            RDGatewayUserViaAPI = Settings.Default.ConDefaultRDGatewayUserViaAPI;
        }

        private void SetAppearanceDefaults()
        {
            Resolution = Enum.TryParse(Settings.Default.ConDefaultResolution, out RDPResolutions res)
                ? res
                : RDPResolutions.SmartSize;
            AutomaticResize = Settings.Default.ConDefaultAutomaticResize;
            Colors = (RDPColors)Enum.Parse(typeof(RDPColors), Settings.Default.ConDefaultColors);
            CacheBitmaps = Settings.Default.ConDefaultCacheBitmaps;
            DisplayWallpaper = Settings.Default.ConDefaultDisplayWallpaper;
            DisplayThemes = Settings.Default.ConDefaultDisplayThemes;
            EnableFontSmoothing = Settings.Default.ConDefaultEnableFontSmoothing;
            EnableDesktopComposition = Settings.Default.ConDefaultEnableDesktopComposition;
            DisableFullWindowDrag = Settings.Default.ConDefaultDisableFullWindowDrag;
            DisableMenuAnimations = Settings.Default.ConDefaultDisableMenuAnimations;
            DisableCursorShadow = Settings.Default.ConDefaultDisableCursorShadow;
            DisableCursorBlinking = Settings.Default.ConDefaultDisableCursorBlinking;
        }

        private void SetRedirectDefaults()
        {
            RedirectKeys = Settings.Default.ConDefaultRedirectKeys;
            RedirectDiskDrives = Settings.Default.ConDefaultRedirectDiskDrives
                ? RDPDiskDrives.All
                : RDPDiskDrives.None;
            RedirectDiskDrivesCustom = Settings.Default.ConDefaultRedirectDiskDrivesCustom;
            RedirectPrinters = Settings.Default.ConDefaultRedirectPrinters;
            RedirectClipboard = Settings.Default.ConDefaultRedirectClipboard;
            RedirectPorts = Settings.Default.ConDefaultRedirectPorts;
            RedirectSmartCards = Settings.Default.ConDefaultRedirectSmartCards;
            RedirectAudioCapture = Settings.Default.ConDefaultRedirectAudioCapture;
            RedirectSound = (RDPSounds)Enum.Parse(typeof(RDPSounds), Settings.Default.ConDefaultRedirectSound);
            SoundQuality = (RDPSoundQuality)Enum.Parse(typeof(RDPSoundQuality), Settings.Default.ConDefaultSoundQuality);
        }

        private void SetMiscDefaults()
        {
            PreExtApp = Settings.Default.ConDefaultPreExtApp;
            PostExtApp = Settings.Default.ConDefaultPostExtApp;
            MacAddress = Settings.Default.ConDefaultMacAddress;
            UserField = Settings.Default.ConDefaultUserField;
            EnvironmentTags = Settings.Default.ConDefaultEnvironmentTags;
            Favorite = Settings.Default.ConDefaultFavorite;
            RDPStartProgram = Settings.Default.ConDefaultRDPStartProgram;
            RDPStartProgramWorkDir = Settings.Default.ConDefaultRDPStartProgramWorkDir;
            OpeningCommand = Settings.Default.OpeningCommand;
        }

        private void SetVncDefaults()
        {
            VNCCompression = (VncCompression)Enum.Parse(typeof(VncCompression), Settings.Default.ConDefaultVNCCompression);
            VNCEncoding = (VncEncoding)Enum.Parse(typeof(VncEncoding), Settings.Default.ConDefaultVNCEncoding);
            VNCAuthMode = (VncAuthMode)Enum.Parse(typeof(VncAuthMode), Settings.Default.ConDefaultVNCAuthMode);
            VNCProxyType = (VncProxyType)Enum.Parse(typeof(VncProxyType), Settings.Default.ConDefaultVNCProxyType);
            VNCProxyIP = Settings.Default.ConDefaultVNCProxyIP;
            VNCProxyPort = Settings.Default.ConDefaultVNCProxyPort;
            VNCProxyUsername = Settings.Default.ConDefaultVNCProxyUsername;
            VNCProxyPassword = Settings.Default.ConDefaultVNCProxyPassword;
            VNCColors = (VncColors)Enum.Parse(typeof(VncColors), Settings.Default.ConDefaultVNCColors);
            VNCSmartSizeMode = (VncSmartSizeMode)Enum.Parse(typeof(VncSmartSizeMode), Settings.Default.ConDefaultVNCSmartSizeMode);
            VNCViewOnly = Settings.Default.ConDefaultVNCViewOnly;
        }

        private void SetNewOpenConnectionList()
        {
            OpenConnections = [];
            OpenConnections.CollectionChanged += (sender, args) => RaisePropertyChangedEvent(this, new PropertyChangedEventArgs("OpenConnections"));
        }

        #endregion
    }
}
