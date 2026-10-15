namespace LoipvRemote.Protocols.Abstractions;

public sealed record RdpRuntimeConfiguration
{
    public string Server { get; init; } = string.Empty;
    public string FullScreenTitle { get; init; } = string.Empty;
    public int IdleTimeoutMinutes { get; init; }
    public string StartProgram { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
    public int MaxReconnectAttempts { get; init; }
    public int OverallConnectionTimeout { get; init; }
    public bool CacheBitmaps { get; init; }
    public bool EnableCredSsp { get; init; }
    public bool ConnectToAdministerServer { get; init; }
    public int Port { get; init; }
    public bool RedirectKeys { get; init; }
    public bool RedirectPorts { get; init; }
    public bool RedirectPrinters { get; init; }
    public bool RedirectSmartCards { get; init; }
    public bool RedirectClipboard { get; init; }
    public int AudioRedirectionMode { get; init; }
    public RdpDriveRedirection DriveRedirection { get; init; }
    public string CustomDrives { get; init; } = string.Empty;
    public uint AuthenticationLevel { get; init; }
    public string LoadBalanceInfo { get; init; } = string.Empty;
    public int ColorDepth { get; init; }
    public int PerformanceFlags { get; init; }
    public string ConnectingText { get; init; } = string.Empty;
    public bool ViewOnly { get; init; }
}

public sealed record RdpCredentialConfiguration(string Username, string Password, string Domain, bool AssignPassword);

public sealed record RdpGatewayConfiguration
{
    public bool Enabled { get; init; }
    public uint UsageMethod { get; init; }
    public string Hostname { get; init; } = string.Empty;
    public bool UseSmartCard { get; init; }
    public bool DisableCredentialSharing { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
}

public sealed record RdpDisplayConfiguration(int Width, int Height, bool FullScreen, bool SmartSize, uint DesktopScaleFactor, uint DeviceScaleFactor);

public enum RdpDriveRedirection { None, All, Custom, Local }
