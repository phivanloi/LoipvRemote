using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Protocols.Rdp;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Rdp;

/// <summary>Creates RDP sessions from Domain connection definitions.</summary>
public sealed class RdpProtocolFactory(
    Func<RdpVersion, IRdpClient> clientFactory,
    Func<IEmbeddedWindowOperations>? windowOperationsFactory = null,
    Func<ConnectionDefinition, string, string?>? secretResolver = null) : IProtocolFactory
{
    private readonly Func<RdpVersion, IRdpClient> _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    private readonly Func<IEmbeddedWindowOperations>? _windowOperationsFactory = windowOperationsFactory;
    private readonly Func<ConnectionDefinition, string, string?>? _secretResolver = secretResolver;

    public IProtocolSession Create(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Protocol != ProtocolKind.Rdp)
            throw new NotSupportedException($"Protocol '{definition.Protocol}' is not handled by {nameof(RdpProtocolFactory)}.");

        ConnectionNodeOptions? values = definition.Options;
        string username = Option(values, "Username");
        string password = _secretResolver?.Invoke(definition, "Password") ?? string.Empty;
        string domain = Option(values, "Domain");
        RdpVersion version = ParseEnum(Option(values, "RdpVersion"), RdpVersion.Rdc10);
        RdpGatewayConfiguration? gateway = BuildGateway(definition, _secretResolver);
        RdpRuntimeConfiguration runtime = BuildRuntimeConfiguration(definition);
        RdpDisplayConfiguration display = BuildDisplayConfiguration(values);
        var options = new RdpConnectionOptions(
            definition.Host,
            definition.Port,
            username,
            password,
            domain,
            gateway,
            runtime,
            display);
        return new RdpProtocolSession(_clientFactory(version), options, _windowOperationsFactory?.Invoke());
    }

    private static RdpDisplayConfiguration BuildDisplayConfiguration(ConnectionNodeOptions? options)
    {
        _ = options;

        // This client is always embedded in a session tab. Negotiate a stable
        // desktop, keep full screen off, and let SmartSizing fit it to the
        // native session host rather than covering the shell/taskbar.
        return new RdpDisplayConfiguration(1920, 1080, false, true, 100, 100);
    }

    private static RdpRuntimeConfiguration BuildRuntimeConfiguration(ConnectionDefinition definition)
    {
        ConnectionNodeOptions? options = definition.Options;
        return new RdpRuntimeConfiguration
        {
            Server = definition.Host,
            FullScreenTitle = definition.Name,
            IdleTimeoutMinutes = ParseInt(Option(options, "RDPMinutesToIdleTimeout"), 0),
            StartProgram = Option(options, "RDPStartProgram"),
            WorkingDirectory = Option(options, "RDPStartProgramWorkDir"),
            MaxReconnectAttempts = 5,
            OverallConnectionTimeout = 20,
            CacheBitmaps = ParseBool(Option(options, "CacheBitmaps"), false),
            EnableCredSsp = ParseBool(Option(options, "UseCredSsp"), true),
            ConnectToAdministerServer = ParseBool(Option(options, "UseConsoleSession"), false),
            Port = definition.Port,
            RedirectKeys = ParseBool(Option(options, "RedirectKeys"), false),
            RedirectPorts = ParseBool(Option(options, "RedirectPorts"), false),
            RedirectPrinters = ParseBool(Option(options, "RedirectPrinters"), false),
            RedirectSmartCards = ParseBool(Option(options, "RedirectSmartCards"), false),
            // Legacy/imported definitions commonly omit redirection options.
            // Keep explicit opt-outs, but make two-way clipboard/file transfer
            // work by default for those existing connections.
            RedirectClipboard = ParseBool(Option(options, "RedirectClipboard"), true),
            AudioRedirectionMode = (int)ParseEnum(Option(options, "RedirectSound"), RDPSounds.DoNotPlay),
            DriveRedirection = ParseEnum(Option(options, "RedirectDiskDrives"), RDPDiskDrives.Local) switch
            {
                RDPDiskDrives.All => RdpDriveRedirection.All,
                RDPDiskDrives.Custom => RdpDriveRedirection.Custom,
                RDPDiskDrives.Local => RdpDriveRedirection.Local,
                _ => RdpDriveRedirection.None
            },
            CustomDrives = Option(options, "RedirectDiskDrivesCustom"),
            AuthenticationLevel = (uint)ParseEnum(
                Option(options, "RDPAuthenticationLevel"),
                AuthenticationLevel.NoAuth),
            LoadBalanceInfo = Option(options, "LoadBalanceInfo"),
            ColorDepth = (int)ParseEnum(Option(options, "Colors"), RDPColors.Colors16Bit),
            PerformanceFlags = CalculatePerformanceFlags(options),
            ConnectingText = "Connecting...",
            ViewOnly = false
        };
    }

    private static int CalculatePerformanceFlags(ConnectionNodeOptions? options)
    {
        RdpPerformanceOptions flags = 0;
        if (!ParseBool(Option(options, "DisplayThemes"), false))
            flags |= RdpPerformanceOptions.DisableThemes;
        if (!ParseBool(Option(options, "DisplayWallpaper"), false))
            flags |= RdpPerformanceOptions.DisableWallpaper;
        if (ParseBool(Option(options, "EnableFontSmoothing"), false))
            flags |= RdpPerformanceOptions.EnableFontSmoothing;
        if (ParseBool(Option(options, "EnableDesktopComposition"), false))
            flags |= RdpPerformanceOptions.EnableDesktopComposition;
        if (ParseBool(Option(options, "DisableFullWindowDrag"), false))
            flags |= RdpPerformanceOptions.DisableFullWindowDrag;
        if (ParseBool(Option(options, "DisableMenuAnimations"), false))
            flags |= RdpPerformanceOptions.DisableMenuAnimations;
        if (ParseBool(Option(options, "DisableCursorShadow"), false))
            flags |= RdpPerformanceOptions.DisableCursorShadow;
        if (ParseBool(Option(options, "DisableCursorBlinking"), false))
            flags |= RdpPerformanceOptions.DisableCursorBlinking;
        return (int)flags;
    }

    private static RdpGatewayConfiguration? BuildGateway(
        ConnectionDefinition definition,
        Func<ConnectionDefinition, string, string?>? secretResolver)
    {
        ConnectionNodeOptions? options = definition.Options;
        string hostname = Option(options, "RDGatewayHostname");
        if (string.IsNullOrWhiteSpace(hostname))
            return null;

        string usage = Option(options, "RDGatewayUsageMethod");
        uint usageMethod = ParseEnumValue<RDGatewayUsageMethod>(usage);
        RDGatewayUseConnectionCredentials credentialMode = ParseEnum(
            Option(options, "RDGatewayUseConnectionCredentials"),
            RDGatewayUseConnectionCredentials.No);
        return new RdpGatewayConfiguration
        {
            Enabled = true,
            Hostname = hostname,
            UsageMethod = usageMethod,
            UseSmartCard = credentialMode == RDGatewayUseConnectionCredentials.SmartCard,
            DisableCredentialSharing = credentialMode != RDGatewayUseConnectionCredentials.Yes,
            Username = Option(options, "RDGatewayUsername"),
            Password = secretResolver?.Invoke(definition, "RDGatewayPassword") ?? string.Empty,
            Domain = Option(options, "RDGatewayDomain")
        };
    }

    private static string Option(ConnectionNodeOptions? options, string name) =>
        options?.Values.TryGetValue(name, out string? value) == true ? value : string.Empty;

    private static bool ParseBool(string value, bool defaultValue) =>
        bool.TryParse(value, out bool parsed) ? parsed : defaultValue;

    private static int ParseInt(string value, int defaultValue) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;

    private static TEnum ParseEnum<TEnum>(string value, TEnum defaultValue)
        where TEnum : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out TEnum parsed) ? parsed : defaultValue;

    private static uint ParseEnumValue<TEnum>(string value)
        where TEnum : struct, Enum =>
        Convert.ToUInt32(ParseEnum(value, default(TEnum)), System.Globalization.CultureInfo.InvariantCulture);
}
