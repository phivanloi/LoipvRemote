namespace LoipvRemote.Protocols.Putty.Monitoring;

/// <summary>Secret-bearing runtime input supplied by the desktop adapter.</summary>
public sealed record PuttyMonitoringConnection(
    string Hostname,
    int Port,
    string Username,
    string Password,
    string DisplayName);

public enum PuttyResourceMonitorState
{
    WaitingForActiveTab,
    Connecting,
    Monitoring,
    AuthenticationUnavailable,
    Unavailable
}

public sealed record PuttyResourceMonitorStatus(
    PuttyResourceMonitorState State,
    string Message,
    string? Fingerprint = null);

public interface IPuttyHostKeyTrustStore
{
    bool IsTrusted(string hostname, int port, string hostKeyName, byte[] hostKey);

    void PreferCachedHostKeyAlgorithms(
        Renci.SshNet.ConnectionInfo connectionInfo,
        string hostname,
        int port);
}
