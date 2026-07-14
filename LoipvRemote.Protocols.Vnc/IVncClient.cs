namespace LoipvRemote.Protocols.Vnc;

/// <summary>Minimal VNC client surface required by the protocol lifecycle.</summary>
public interface IVncClient
{
    void SetPort(int port);
    void Connect(string host, bool viewOnly, bool smartSize);
    void Disconnect();
}
