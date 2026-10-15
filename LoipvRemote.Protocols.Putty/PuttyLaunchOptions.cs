namespace LoipvRemote.Protocols.Putty;

public sealed record PuttyLaunchOptions
{
    public string SavedSession { get; init; } = string.Empty;
    public bool UseSavedSessionOnly { get; init; }
    public PuttyProtocolKind Protocol { get; init; }
    public PuttySshVersion SshVersion { get; init; }
    public string Username { get; init; } = string.Empty;
    public string PasswordPipeName { get; init; } = string.Empty;
    public string PrivateKeyPath { get; init; } = string.Empty;
    public string OpeningCommandPath { get; init; } = string.Empty;
    public string AuthenticationPluginCommand { get; init; } = string.Empty;
    public string SessionLogPath { get; init; } = string.Empty;
    public bool SuppressCredentials { get; init; }
    public int Port { get; init; }
    public string Hostname { get; init; } = string.Empty;
    public nint ParentWindowHandle { get; init; }
    public string AdditionalOptions { get; init; } = string.Empty;
}
