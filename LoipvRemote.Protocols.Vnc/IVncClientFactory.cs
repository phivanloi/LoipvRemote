namespace LoipvRemote.Protocols.Vnc;

/// <summary>Creates a VNC renderer without exposing it to the desktop shell.</summary>
public interface IVncClientFactory
{
    IVncClient Create();
}
