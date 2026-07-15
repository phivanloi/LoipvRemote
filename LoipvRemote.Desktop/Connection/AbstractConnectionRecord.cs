using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using LoipvRemote.Properties;
using LoipvRemote.Tools;
using LoipvRemote.Tools.Attributes;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;
using System.Security;

namespace LoipvRemote.Connection
{
    [SupportedOSPlatform("windows")]
    public abstract class AbstractConnectionRecord(string uniqueId) : INotifyPropertyChanged
    {
        #region Fields

        private string _name = "";
        private string _description = "";
        private string _icon = "";
        private string _panel = "";
        private string _color = "";
        private string _tabColor = "";
        private ConnectionFrameColor _connectionFrameColor;

        private string _hostname = "";
        private ExternalAddressProvider _externalAddressProvider;
        private string _ec2InstanceId = "";
        private string _ec2Region = "";
        private ExternalCredentialProvider _externalCredentialProvider;
        private string _userViaAPI = "";
        private string _username = "";
        //private SecureString _password = null;
        private string _password = "";
        private string _vaultRole = "";
        private string _vaultMount = "";
        private VaultOpenbaoSecretEngine _vaultSecretEngine = VaultOpenbaoSecretEngine.Kv;
        private string _domain = "";
        private string _vmId = "";
        private bool _useEnhancedMode;

        private string _sshTunnelConnectionName = "";
        private ProtocolKind _protocol;
        private RdpVersion _rdpProtocolVersion = RdpVersion.Rdc10;
        private string _extApp = "";
        private int _port;
        private string _sshOptions = "";
        private string _puttySession = "";
        private bool _useConsoleSession;
        private AuthenticationLevel _rdpAuthenticationLevel;
        private int _rdpMinutesToIdleTimeout;
        private bool _rdpAlertIdleTimeout;
        private string _loadBalanceInfo = "";
        private BrowserRenderingEngine _renderingEngine;
        private bool _useCredSsp;
        private bool _useRestrictedAdmin;
        private bool _useRCG;
        private bool _useVmId;
        private bool _useRedirectionServerName;

        private RDGatewayUsageMethod _rdGatewayUsageMethod;
        private string _rdGatewayHostname = "";
        private RDGatewayUseConnectionCredentials _rdGatewayUseConnectionCredentials;
        private string _rdGatewayUsername = "";
        private string _rdGatewayPassword = "";
        private string _rdGatewayDomain = "";
        private string _rdGatewayAccessToken = "";
        private ExternalCredentialProvider _rdGatewayExternalCredentialProvider;
        private string _rdGatewayUserViaAPI = "";


        private RDPResolutions _resolution;
        private bool _automaticResize;
        private RDPColors _colors;
        private bool _cacheBitmaps;
        private bool _displayWallpaper;
        private bool _displayThemes;
        private bool _enableFontSmoothing;
        private bool _enableDesktopComposition;
        private bool _disableFullWindowDrag;
        private bool _disableMenuAnimations;
        private bool _disableCursorShadow;
        private bool _disableCursorBlinking;

        private bool _redirectKeys;
        private RDPDiskDrives _redirectDiskDrives;
        private string _redirectDiskDrivesCustom = "";
        private bool _redirectPrinters;
        private bool _redirectClipboard;
        private bool _redirectPorts;
        private bool _redirectSmartCards;
        private RDPSounds _redirectSound;
        private RDPSoundQuality _soundQuality;
        private bool _redirectAudioCapture;

        private string _preExtApp = "";
        private string _postExtApp = "";
        private string _macAddress = "";
        private string _openingCommand = "";
        private string _userField = "";
        private string _environmentTags = "";
        private string _rdpStartProgram = "";
        private string _rdpStartProgramWorkDir = "";
        private bool _favorite;

        private VncCompression _vncCompression;
        private VncEncoding _vncEncoding;
        private VncAuthMode _vncAuthMode;
        private VncProxyType _vncProxyType;
        private string _vncProxyIp = "";
        private int _vncProxyPort;
        private string _vncProxyUsername = "";
        private string _vncProxyPassword = "";
        private VncColors _vncColors;
        private VncSmartSizeMode _vncSmartSizeMode;
        private bool _vncViewOnly;

        #endregion

        #region Properties

        #region Display

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Display)),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Name)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionName))]
        public virtual string Name
        {
            get => _name;
            set => SetField(ref _name, value, nameof(Name));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Display)),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Description)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionDescription))]
        public virtual string Description
        {
            get => GetPropertyValue(nameof(Description), _description);
            set => SetField(ref _description, value, nameof(Description));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Display)),
         TypeConverter(typeof(ConnectionIcon)),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Icon)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionIcon))]
        public virtual string Icon
        {
            get => GetPropertyValue(nameof(Icon), _icon);
            set => SetField(ref _icon, value, nameof(Icon));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Display)),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Panel)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionPanel))]
        public virtual string Panel
        {
            get => GetPropertyValue(nameof(Panel), _panel);
            set => SetField(ref _panel, value, nameof(Panel));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Display)),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Color)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionColor)),
         Editor(typeof(System.Drawing.Design.ColorEditor), typeof(System.Drawing.Design.UITypeEditor)),
         TypeConverter(typeof(MiscTools.TabColorConverter))]
        public virtual string Color
        {
            get => GetPropertyValue(nameof(Color), _color);
            set => SetField(ref _color, value, nameof(Color));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Display)),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.TabColor)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionTabColor)),
         Editor(typeof(System.Drawing.Design.ColorEditor), typeof(System.Drawing.Design.UITypeEditor)),
         TypeConverter(typeof(MiscTools.TabColorConverter))]
        public virtual string TabColor
        {
            get => GetPropertyValue(nameof(TabColor), _tabColor);
            set => SetField(ref _tabColor, value, nameof(TabColor));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Display)),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.ConnectionFrameColor)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionConnectionFrameColor)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter))]
        public virtual ConnectionFrameColor ConnectionFrameColor
        {
            get => GetPropertyValue(nameof(ConnectionFrameColor), _connectionFrameColor);
            set => SetField(ref _connectionFrameColor, value, nameof(ConnectionFrameColor));
        }

        #endregion

        #region Connection

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.HostnameIp)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionHostnameIp)),
         AttributeUsedInAllProtocolsExcept()]
        public virtual string Hostname
        {
            get => _hostname?.Trim() ?? string.Empty;
            set => SetField(ref _hostname, value?.Trim(), nameof(Hostname));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Port)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionPort)),
         AttributeUsedInAllProtocolsExcept()]
        public virtual int Port
        {
            get => GetPropertyValue(nameof(Port), _port);
            set => SetField(ref _port, value, nameof(Port));
        }

        // external credential provider selector
        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.ExternalCredentialProvider)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionExternalCredentialProvider)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp, ProtocolKind.Ssh1, ProtocolKind.Ssh2)]
        public ExternalCredentialProvider ExternalCredentialProvider
        {
            get => GetPropertyValue(nameof(ExternalCredentialProvider), _externalCredentialProvider);
            set => SetField(ref _externalCredentialProvider, value, nameof(ExternalCredentialProvider));
        }

        // credential record identifier for external credential provider
        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.UserViaAPI)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionUserViaAPI)),
         AttributeUsedInProtocol(ProtocolKind.Rdp, ProtocolKind.Ssh1, ProtocolKind.Ssh2)]
        public virtual string UserViaAPI
        {
            get => GetPropertyValue(nameof(UserViaAPI), _userViaAPI);
            set => SetField(ref _userViaAPI, value, nameof(UserViaAPI));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Username)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionUsername)),
         AttributeUsedInProtocol(ProtocolKind.Rdp, ProtocolKind.Ssh1, ProtocolKind.Ssh2, ProtocolKind.Http, ProtocolKind.Https, ProtocolKind.ExternalApplication)]
        public virtual string Username
        {
            get => GetPropertyValue(nameof(Username), _username);
            set => SetField(ref _username, Settings.Default.DoNotTrimUsername ? value : value?.Trim(), nameof(Username));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Password)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionPassword)),
         PasswordPropertyText(true),
         AttributeUsedInAllProtocolsExcept(ProtocolKind.Telnet, ProtocolKind.Rlogin, ProtocolKind.Raw)]
        //public virtual SecureString Password
        public virtual string Password
        {
            get => GetPropertyValue(nameof(Password), _password);
            set => SetField(ref _password, value, nameof(Password));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.VaultOpenbaoMount)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.VaultOpenbaoMountDescription)),
         AttributeUsedInProtocol(ProtocolKind.Rdp, ProtocolKind.Ssh1, ProtocolKind.Ssh2)]
        public virtual string VaultOpenbaoMount {
            get => GetPropertyValue(nameof(VaultOpenbaoMount), _vaultMount);
            set => SetField(ref _vaultMount, value, nameof(VaultOpenbaoMount));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.VaultOpenbaoRole)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.VaultOpenbaoRoleDescription)),
         AttributeUsedInProtocol(ProtocolKind.Rdp, ProtocolKind.Ssh1, ProtocolKind.Ssh2)]
        public virtual string VaultOpenbaoRole {
            get => GetPropertyValue(nameof(VaultOpenbaoRole), _vaultRole);
            set => SetField(ref _vaultRole, value, nameof(VaultOpenbaoRole));
        }

        // external credential provider selector
        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.VaultOpenbaoSecretEngine)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionVaultOpenbaoSecretEngine)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp, ProtocolKind.Ssh1, ProtocolKind.Ssh2)]
        public VaultOpenbaoSecretEngine VaultOpenbaoSecretEngine {
            get => GetPropertyValue(nameof(VaultOpenbaoSecretEngine), _vaultSecretEngine);
            set => SetField(ref _vaultSecretEngine, value, nameof(VaultOpenbaoSecretEngine));
        }


        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Domain)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionDomain)),
         AttributeUsedInProtocol(ProtocolKind.Rdp, ProtocolKind.ExternalApplication, ProtocolKind.PowerShell, ProtocolKind.Wsl)]
        public string Domain
        {
            get => GetPropertyValue(nameof(Domain), _domain).Trim();
            set => SetField(ref _domain, value?.Trim(), nameof(Domain));
        }


        // external address provider selector
        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.ExternalAddressProvider)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionExternalAddressProvider)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp, ProtocolKind.Ssh2)]
        public ExternalAddressProvider ExternalAddressProvider
        {
            get => GetPropertyValue(nameof(ExternalAddressProvider), _externalAddressProvider);
            set => SetField(ref _externalAddressProvider, value, nameof(ExternalAddressProvider));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
        LocalizedAttributes.LocalizedDisplayName(nameof(Language.EC2InstanceId)),
        LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionEC2InstanceId)),
        AttributeUsedInProtocol(ProtocolKind.Rdp, ProtocolKind.Ssh2)]
        public string EC2InstanceId
        {
            get => GetPropertyValue(nameof(EC2InstanceId), _ec2InstanceId).Trim();
            set => SetField(ref _ec2InstanceId, value?.Trim(), nameof(EC2InstanceId));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
        LocalizedAttributes.LocalizedDisplayName(nameof(Language.EC2Region)),
        LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionEC2Region)),
        AttributeUsedInProtocol(ProtocolKind.Rdp, ProtocolKind.Ssh2)]
        public string EC2Region
        {
            get => GetPropertyValue(nameof(EC2Region), _ec2Region).Trim();
            set => SetField(ref _ec2Region, value?.Trim(), nameof(EC2Region));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.VmId)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionVmId)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public string VmId
        {
            get => GetPropertyValue(nameof(VmId), _vmId).Trim();
            set => SetField(ref _vmId, value?.Trim(), nameof(VmId));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.SshTunnel)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionSshTunnel)),
         TypeConverter(typeof(SshTunnelTypeConverter)),
         AttributeUsedInAllProtocolsExcept()]
        public string SSHTunnelConnectionName
        {
            get => GetPropertyValue(nameof(SSHTunnelConnectionName), _sshTunnelConnectionName).Trim();
            set => SetField(ref _sshTunnelConnectionName, value?.Trim(), nameof(SSHTunnelConnectionName));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Miscellaneous), 7),
        LocalizedAttributes.LocalizedDisplayName(nameof(Language.OpeningCommand)),
        LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionOpeningCommand)),
           AttributeUsedInProtocol(ProtocolKind.Ssh1, ProtocolKind.Ssh2)]
        public virtual string OpeningCommand
        {
            get => GetPropertyValue(nameof(OpeningCommand), _openingCommand);
            set => SetField(ref _openingCommand, value, nameof(OpeningCommand));
        }
        #endregion

        #region Protocol

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Protocol)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionProtocol)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter))]
        public virtual ProtocolKind Protocol
        {
            get => GetPropertyValue(nameof(Protocol), _protocol);
            set => SetField(ref _protocol, value, nameof(Protocol));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.RdpVersion)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRdpVersion)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public virtual RdpVersion RdpVersion
        {
            get => GetPropertyValue(nameof(RdpVersion), _rdpProtocolVersion);
            set => SetField(ref _rdpProtocolVersion, value, nameof(RdpVersion));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.ExternalTool)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionExternalTool)),
         TypeConverter(typeof(ExternalToolsTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.ExternalApplication)]
        public string ExtApp
        {
            get => GetPropertyValue(nameof(ExtApp), _extApp);
            set => SetField(ref _extApp, value, nameof(ExtApp));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.PuttySession)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionPuttySession)),
         TypeConverter(typeof(Config.Putty.PuttySessionsManager.SessionList)),
         AttributeUsedInProtocol(ProtocolKind.Ssh1, ProtocolKind.Ssh2, ProtocolKind.Telnet,
            ProtocolKind.Raw, ProtocolKind.Rlogin)]
        public virtual string PuttySession
        {
            get => GetPropertyValue(nameof(PuttySession), _puttySession);
            set => SetField(ref _puttySession, value, nameof(PuttySession));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.SshOptions)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionSshOptions)),
         AttributeUsedInProtocol(ProtocolKind.Ssh1, ProtocolKind.Ssh2)]
        public virtual string SSHOptions
        {
            get => GetPropertyValue(nameof(SSHOptions), _sshOptions);
            set => SetField(ref _sshOptions, value, nameof(SSHOptions));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.UseConsoleSession)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionUseConsoleSession)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool UseConsoleSession
        {
            get => GetPropertyValue(nameof(UseConsoleSession), _useConsoleSession);
            set => SetField(ref _useConsoleSession, value, nameof(UseConsoleSession));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.AuthenticationLevel)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionAuthenticationLevel)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public AuthenticationLevel RDPAuthenticationLevel
        {
            get => GetPropertyValue(nameof(RDPAuthenticationLevel), _rdpAuthenticationLevel);
            set => SetField(ref _rdpAuthenticationLevel, value, nameof(RDPAuthenticationLevel));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.MinutesToIdleTimeout)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRDPMinutesToIdleTimeout)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public virtual int RDPMinutesToIdleTimeout
        {
            get => GetPropertyValue(nameof(RDPMinutesToIdleTimeout), _rdpMinutesToIdleTimeout);
            set
            {
                if (value < 0)
                    value = 0;
                else if (value > 240)
                    value = 240;
                SetField(ref _rdpMinutesToIdleTimeout, value, nameof(RDPMinutesToIdleTimeout));
            }
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.MinutesToIdleTimeout)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRDPAlertIdleTimeout)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool RDPAlertIdleTimeout
        {
            get => GetPropertyValue(nameof(RDPAlertIdleTimeout), _rdpAlertIdleTimeout);
            set => SetField(ref _rdpAlertIdleTimeout, value, nameof(RDPAlertIdleTimeout));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.LoadBalanceInfo)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionLoadBalanceInfo)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public string LoadBalanceInfo
        {
            get => GetPropertyValue(nameof(LoadBalanceInfo), _loadBalanceInfo).Trim();
            set => SetField(ref _loadBalanceInfo, value?.Trim(), nameof(LoadBalanceInfo));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.RenderingEngine)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRenderingEngine)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Http, ProtocolKind.Https)]
        public BrowserRenderingEngine RenderingEngine
        {
            get => GetPropertyValue(nameof(RenderingEngine), _renderingEngine);
            set => SetField(ref _renderingEngine, value, nameof(RenderingEngine));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.UseCredSsp)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionUseCredSsp)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool UseCredSsp
        {
            get => GetPropertyValue(nameof(UseCredSsp), _useCredSsp);
            set => SetField(ref _useCredSsp, value, nameof(UseCredSsp));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.UseRestrictedAdmin)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionUseRestrictedAdmin)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool UseRestrictedAdmin
        {
            get => GetPropertyValue(nameof(UseRestrictedAdmin), _useRestrictedAdmin);
            set => SetField(ref _useRestrictedAdmin, value, nameof(UseRestrictedAdmin));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.UseRCG)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionUseRCG)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool UseRCG
        {
            get => GetPropertyValue(nameof(UseRCG), _useRCG);
            set => SetField(ref _useRCG, value, nameof(UseRCG));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.UseRedirectionServerName)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionUseRedirectionServerName)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool UseRedirectionServerName
        {
            get => GetPropertyValue(nameof(UseRedirectionServerName), _useRedirectionServerName);
            set => SetField(ref _useRedirectionServerName, value, nameof(UseRedirectionServerName));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.UseVmId)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionUseVmId)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool UseVmId
        {
            get => GetPropertyValue(nameof(UseVmId), _useVmId);
            set => SetField(ref _useVmId, value, nameof(UseVmId));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Protocol), 3),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.UseEnhancedMode)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionUseEnhancedMode)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool UseEnhancedMode
        {
            get => GetPropertyValue(nameof(UseEnhancedMode), _useEnhancedMode);
            set => SetField(ref _useEnhancedMode, value, nameof(UseEnhancedMode));
        }
        #endregion

        #region RD Gateway

        [LocalizedAttributes.LocalizedCategory(nameof(Language.RDPGateway), 4),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.RdpGatewayUsageMethod)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRdpGatewayUsageMethod)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public RDGatewayUsageMethod RDGatewayUsageMethod
        {
            get => GetPropertyValue(nameof(RDGatewayUsageMethod), _rdGatewayUsageMethod);
            set => SetField(ref _rdGatewayUsageMethod, value, nameof(RDGatewayUsageMethod));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.RDPGateway), 4),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.RdpGatewayHostname)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRDGatewayHostname)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public string RDGatewayHostname
        {
            get => GetPropertyValue(nameof(RDGatewayHostname), _rdGatewayHostname).Trim();
            set => SetField(ref _rdGatewayHostname, value?.Trim(), nameof(RDGatewayHostname));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.RDPGateway), 4),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.RdpGatewayUseConnectionCredentials)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRDGatewayUseConnectionCredentials)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public RDGatewayUseConnectionCredentials RDGatewayUseConnectionCredentials
        {
            get => GetPropertyValue(nameof(RDGatewayUseConnectionCredentials), _rdGatewayUseConnectionCredentials);
            set => SetField(ref _rdGatewayUseConnectionCredentials, value, nameof(RDGatewayUseConnectionCredentials));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.RDPGateway), 4),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.RdpGatewayUsername)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRDGatewayUsername)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public string RDGatewayUsername
        {
            get => GetPropertyValue(nameof(RDGatewayUsername), _rdGatewayUsername).Trim();
            set => SetField(ref _rdGatewayUsername, value?.Trim(), nameof(RDGatewayUsername));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.RDPGateway), 4),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.RdpGatewayPassword)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRdpGatewayPassword)),
         PasswordPropertyText(true),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public string RDGatewayPassword
        {
            get => GetPropertyValue(nameof(RDGatewayPassword), _rdGatewayPassword);
            set => SetField(ref _rdGatewayPassword, value, nameof(RDGatewayPassword));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.RDPGateway), 4),
        LocalizedAttributes.LocalizedDisplayName(nameof(Language.RdpGatewayAccessToken)),
        LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRdpGatewayAccessToken)),
        PasswordPropertyText(true),
        AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public string RDGatewayAccessToken
        {
            get => GetPropertyValue(nameof(RDGatewayAccessToken), _rdGatewayAccessToken);
            set => SetField(ref _rdGatewayAccessToken, value, nameof(RDGatewayAccessToken));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.RDPGateway), 4),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.RdpGatewayDomain)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRDGatewayDomain)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public string RDGatewayDomain
        {
            get => GetPropertyValue(nameof(RDGatewayDomain), _rdGatewayDomain).Trim();
            set => SetField(ref _rdGatewayDomain, value?.Trim(), nameof(RDGatewayDomain));
        }
        // external credential provider selector for rd gateway
        [LocalizedAttributes.LocalizedCategory(nameof(Language.RDPGateway), 4),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.ExternalCredentialProvider)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionExternalCredentialProvider)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public ExternalCredentialProvider RDGatewayExternalCredentialProvider
        {
            get => GetPropertyValue(nameof(RDGatewayExternalCredentialProvider), _rdGatewayExternalCredentialProvider);
            set => SetField(ref _rdGatewayExternalCredentialProvider, value, nameof(RDGatewayExternalCredentialProvider));
        }

        // credential record identifier for external credential provider
        [LocalizedAttributes.LocalizedCategory(nameof(Language.RDPGateway), 4),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.UserViaAPI)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionUserViaAPI)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public virtual string RDGatewayUserViaAPI
        {
            get => GetPropertyValue(nameof(RDGatewayUserViaAPI), _rdGatewayUserViaAPI);
            set => SetField(ref _rdGatewayUserViaAPI, value, nameof(RDGatewayUserViaAPI));
        }
        #endregion

        #region Appearance

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Resolution)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionResolution)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public RDPResolutions Resolution
        {
            get => GetPropertyValue(nameof(Resolution), _resolution);
            set => SetField(ref _resolution, value, nameof(Resolution));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.AutomaticResize)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionAutomaticResize)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool AutomaticResize
        {
            get => GetPropertyValue(nameof(AutomaticResize), _automaticResize);
            set => SetField(ref _automaticResize, value, nameof(AutomaticResize));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Colors)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionColors)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public RDPColors Colors
        {
            get => GetPropertyValue(nameof(Colors), _colors);
            set => SetField(ref _colors, value, nameof(Colors));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.CacheBitmaps)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionCacheBitmaps)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool CacheBitmaps
        {
            get => GetPropertyValue(nameof(CacheBitmaps), _cacheBitmaps);
            set => SetField(ref _cacheBitmaps, value, nameof(CacheBitmaps));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.DisplayWallpaper)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionDisplayWallpaper)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool DisplayWallpaper
        {
            get => GetPropertyValue(nameof(DisplayWallpaper), _displayWallpaper);
            set => SetField(ref _displayWallpaper, value, nameof(DisplayWallpaper));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.DisplayThemes)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionDisplayThemes)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool DisplayThemes
        {
            get => GetPropertyValue(nameof(DisplayThemes), _displayThemes);
            set => SetField(ref _displayThemes, value, nameof(DisplayThemes));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.FontSmoothing)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionEnableFontSmoothing)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool EnableFontSmoothing
        {
            get => GetPropertyValue(nameof(EnableFontSmoothing), _enableFontSmoothing);
            set => SetField(ref _enableFontSmoothing, value, nameof(EnableFontSmoothing));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.EnableDesktopComposition)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionEnableDesktopComposition)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool EnableDesktopComposition
        {
            get => GetPropertyValue(nameof(EnableDesktopComposition), _enableDesktopComposition);
            set => SetField(ref _enableDesktopComposition, value, nameof(EnableDesktopComposition));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.DisableFullWindowDrag)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionDisableFullWindowDrag)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool DisableFullWindowDrag
        {
            get => GetPropertyValue(nameof(DisableFullWindowDrag), _disableFullWindowDrag);
            set => SetField(ref _disableFullWindowDrag, value, nameof(DisableFullWindowDrag));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.DisableMenuAnimations)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionDisableMenuAnimations)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool DisableMenuAnimations
        {
            get => GetPropertyValue(nameof(DisableMenuAnimations), _disableMenuAnimations);
            set => SetField(ref _disableMenuAnimations, value, nameof(DisableMenuAnimations));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.DisableCursorShadow)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionDisableCursorShadow)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool DisableCursorShadow
        {
            get => GetPropertyValue(nameof(DisableCursorShadow), _disableCursorShadow);
            set => SetField(ref _disableCursorShadow, value, nameof(DisableCursorShadow));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.DisableCursorShadow)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionDisableCursorShadow)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool DisableCursorBlinking
        {
            get => GetPropertyValue(nameof(DisableCursorBlinking), _disableCursorBlinking);
            set => SetField(ref _disableCursorBlinking, value, nameof(DisableCursorBlinking));
        }
        #endregion

        #region Redirect

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Redirect), 6),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.RedirectKeys)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRedirectKeys)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool RedirectKeys
        {
            get => GetPropertyValue(nameof(RedirectKeys), _redirectKeys);
            set => SetField(ref _redirectKeys, value, nameof(RedirectKeys));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Redirect), 6),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.DiskDrives)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRedirectDrives)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public RDPDiskDrives RedirectDiskDrives
        {
            get => GetPropertyValue(nameof(RedirectDiskDrives), _redirectDiskDrives);
            set => SetField(ref _redirectDiskDrives, value, nameof(RedirectDiskDrives));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Redirect), 6),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.RedirectDiskDrivesCustom)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRedirectDiskDrivesCustom)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public string RedirectDiskDrivesCustom
        {
            get => GetPropertyValue(nameof(RedirectDiskDrivesCustom), _redirectDiskDrivesCustom);
            set => SetField(ref _redirectDiskDrivesCustom, value, nameof(RedirectDiskDrivesCustom));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Redirect), 6),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Printers)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRedirectPrinters)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool RedirectPrinters
        {
            get => GetPropertyValue(nameof(RedirectPrinters), _redirectPrinters);
            set => SetField(ref _redirectPrinters, value, nameof(RedirectPrinters));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Redirect), 6),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Clipboard)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRedirectClipboard)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool RedirectClipboard
        {
            get => GetPropertyValue(nameof(RedirectClipboard), _redirectClipboard);
            set => SetField(ref _redirectClipboard, value, nameof(RedirectClipboard));
        }


        [LocalizedAttributes.LocalizedCategory(nameof(Language.Redirect), 6),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Ports)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRedirectPorts)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool RedirectPorts
        {
            get => GetPropertyValue(nameof(RedirectPorts), _redirectPorts);
            set => SetField(ref _redirectPorts, value, nameof(RedirectPorts));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Redirect), 6),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.SmartCard)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRedirectSmartCards)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool RedirectSmartCards
        {
            get => GetPropertyValue(nameof(RedirectSmartCards), _redirectSmartCards);
            set => SetField(ref _redirectSmartCards, value, nameof(RedirectSmartCards));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Redirect), 6),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Sounds)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRedirectSounds)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public RDPSounds RedirectSound
        {
            get => GetPropertyValue(nameof(RedirectSound), _redirectSound);
            set => SetField(ref _redirectSound, value, nameof(RedirectSound));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Redirect), 6),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.SoundQuality)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionSoundQuality)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public RDPSoundQuality SoundQuality
        {
            get => GetPropertyValue(nameof(SoundQuality), _soundQuality);
            set => SetField(ref _soundQuality, value, nameof(SoundQuality));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Redirect), 6),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.AudioCapture)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRedirectAudioCapture)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public bool RedirectAudioCapture
        {
            get => GetPropertyValue(nameof(RedirectAudioCapture), _redirectAudioCapture);
            set => SetField(ref _redirectAudioCapture, value, nameof(RedirectAudioCapture));
        }

        #endregion

        #region Misc

        [Browsable(false)] public string ConstantID { get; } = uniqueId.ThrowIfNullOrEmpty(nameof(uniqueId));

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Miscellaneous), 7),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.ExternalToolBefore)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionExternalToolBefore)),
         TypeConverter(typeof(ExternalToolsTypeConverter))]
        public virtual string PreExtApp
        {
            get => GetPropertyValue(nameof(PreExtApp), _preExtApp);
            set => SetField(ref _preExtApp, value, nameof(PreExtApp));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Miscellaneous), 7),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.ExternalToolAfter)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionExternalToolAfter)),
         TypeConverter(typeof(ExternalToolsTypeConverter))]
        public virtual string PostExtApp
        {
            get => GetPropertyValue(nameof(PostExtApp), _postExtApp);
            set => SetField(ref _postExtApp, value, nameof(PostExtApp));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Miscellaneous), 7),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.MacAddress)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionMACAddress))]
        public virtual string MacAddress
        {
            get => GetPropertyValue(nameof(MacAddress), _macAddress);
            set => SetField(ref _macAddress, value, nameof(MacAddress));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Miscellaneous), 7),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.UserField)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionUser1))]
        public virtual string UserField
        {
            get => GetPropertyValue(nameof(UserField), _userField);
            set => SetField(ref _userField, value, nameof(UserField));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Miscellaneous), 7),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.EnvironmentTags)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionEnvironmentTags))]
        public virtual string EnvironmentTags
        {
            get => GetPropertyValue(nameof(EnvironmentTags), _environmentTags);
            set => SetField(ref _environmentTags, value, nameof(EnvironmentTags));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Miscellaneous), 7),
            LocalizedAttributes.LocalizedDisplayName(nameof(Language.Favorite)),
            LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionFavorite)),
            TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
        public virtual bool Favorite
        {
            get => GetPropertyValue(nameof(Favorite), _favorite);
            set => SetField(ref _favorite, value, nameof(Favorite));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Miscellaneous), 7),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.RDPStartProgram)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRDPStartProgram)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public virtual string RDPStartProgram
        {
            get => GetPropertyValue(nameof(RDPStartProgram), _rdpStartProgram);
            set => SetField(ref _rdpStartProgram, value, nameof(RDPStartProgram));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Miscellaneous), 7),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.RDPStartProgramWorkDir)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionRDPStartProgramWorkDir)),
         AttributeUsedInProtocol(ProtocolKind.Rdp)]
        public virtual string RDPStartProgramWorkDir
        {
            get => GetPropertyValue(nameof(RDPStartProgramWorkDir), _rdpStartProgramWorkDir);
            set => SetField(ref _rdpStartProgramWorkDir, value, nameof(RDPStartProgramWorkDir));
        }

        #endregion

        #region VNC
        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Compression)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionCompression)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Vnc, ProtocolKind.Ard),
         Browsable(false)]
        public VncCompression VNCCompression
        {
            get => GetPropertyValue(nameof(VNCCompression), _vncCompression);
            set => SetField(ref _vncCompression, value, nameof(VNCCompression));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Encoding)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionEncoding)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Vnc, ProtocolKind.Ard),
         Browsable(false)]
        public VncEncoding VNCEncoding
        {
            get => GetPropertyValue(nameof(VNCEncoding), _vncEncoding);
            set => SetField(ref _vncEncoding, value, nameof(VNCEncoding));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Connection), 2),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.AuthenticationMode)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionAuthenticationMode)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Vnc, ProtocolKind.Ard),
         Browsable(false)]
        public VncAuthMode VNCAuthMode
        {
            get => GetPropertyValue(nameof(VNCAuthMode), _vncAuthMode);
            set => SetField(ref _vncAuthMode, value, nameof(VNCAuthMode));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Proxy), 7),
            LocalizedAttributes.LocalizedDisplayName(nameof(Language.ProxyType)),
            LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionVNCProxyType)),
            TypeConverter(typeof(MiscTools.EnumTypeConverter)),
            AttributeUsedInProtocol(ProtocolKind.Vnc, ProtocolKind.Ard),
            Browsable(false)]
        public VncProxyType VNCProxyType
        {
            get => GetPropertyValue(nameof(VNCProxyType), _vncProxyType);
            set => SetField(ref _vncProxyType, value, nameof(VNCProxyType));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Proxy), 7),
            LocalizedAttributes.LocalizedDisplayName(nameof(Language.ProxyAddress)),
            LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionVNCProxyAddress)),
            AttributeUsedInProtocol(ProtocolKind.Vnc, ProtocolKind.Ard),
            Browsable(false)]
        public string VNCProxyIP
        {
            get => GetPropertyValue(nameof(VNCProxyIP), _vncProxyIp);
            set => SetField(ref _vncProxyIp, value, nameof(VNCProxyIP));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Proxy), 7),
            LocalizedAttributes.LocalizedDisplayName(nameof(Language.ProxyPort)),
            LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionVNCProxyPort)),
            AttributeUsedInProtocol(ProtocolKind.Vnc, ProtocolKind.Ard),
            Browsable(false)]
        public int VNCProxyPort
        {
            get => GetPropertyValue(nameof(VNCProxyPort), _vncProxyPort);
            set => SetField(ref _vncProxyPort, value, nameof(VNCProxyPort));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Proxy), 7),
            LocalizedAttributes.LocalizedDisplayName(nameof(Language.ProxyUsername)),
            LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionVNCProxyUsername)),
            AttributeUsedInProtocol(ProtocolKind.Vnc, ProtocolKind.Ard),
            Browsable(false)]
        public string VNCProxyUsername
        {
            get => GetPropertyValue(nameof(VNCProxyUsername), _vncProxyUsername);
            set => SetField(ref _vncProxyUsername, value, nameof(VNCProxyUsername));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Proxy), 7),
            LocalizedAttributes.LocalizedDisplayName(nameof(Language.ProxyPassword)),
            LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionVNCProxyPassword)),
            PasswordPropertyText(true),
            AttributeUsedInProtocol(ProtocolKind.Vnc, ProtocolKind.Ard),
            Browsable(false)]
        public string VNCProxyPassword
        {
            get => GetPropertyValue(nameof(VNCProxyPassword), _vncProxyPassword);
            set => SetField(ref _vncProxyPassword, value, nameof(VNCProxyPassword));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Colors)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionColors)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Vnc, ProtocolKind.Ard),
         Browsable(false)]
        public VncColors VNCColors
        {
            get => GetPropertyValue(nameof(VNCColors), _vncColors);
            set => SetField(ref _vncColors, value, nameof(VNCColors));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.SmartSizeMode)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionSmartSizeMode)),
         TypeConverter(typeof(MiscTools.EnumTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Vnc, ProtocolKind.Ard)]
        public VncSmartSizeMode VNCSmartSizeMode
        {
            get => GetPropertyValue(nameof(VNCSmartSizeMode), _vncSmartSizeMode);
            set => SetField(ref _vncSmartSizeMode, value, nameof(VNCSmartSizeMode));
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Appearance), 5),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.ViewOnly)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionViewOnly)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter)),
         AttributeUsedInProtocol(ProtocolKind.Vnc, ProtocolKind.Ard)]
        public bool VNCViewOnly
        {
            get => GetPropertyValue(nameof(VNCViewOnly), _vncViewOnly);
            set => SetField(ref _vncViewOnly, value, nameof(VNCViewOnly));
        }

        #endregion
        #endregion

        protected virtual TPropertyType GetPropertyValue<TPropertyType>(string propertyName, TPropertyType value)
        {
            return (TPropertyType)GetType().GetProperty(propertyName)?.GetValue(this, null);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void RaisePropertyChangedEvent(object? sender, PropertyChangedEventArgs args)
        {
            PropertyChanged?.Invoke(sender, new PropertyChangedEventArgs(args.PropertyName));
        }

        protected void SetField<T>(ref T field, T value, string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            RaisePropertyChangedEvent(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
