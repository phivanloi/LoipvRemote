namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Minimal RDP transport surface required by the protocol lifecycle.</summary>
public interface IRdpClient
{
    void Connect();
    void Disconnect();
}
