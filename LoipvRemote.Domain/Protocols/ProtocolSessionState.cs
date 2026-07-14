namespace LoipvRemote.Domain.Protocols;

/// <summary>The lifecycle state of a remote protocol session.</summary>
public enum ProtocolSessionState
{
    Created,
    Initialized,
    Connected,
    Closing,
    Closed,
    Faulted
}
