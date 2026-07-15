namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Optional screen refresh capability exposed to the desktop shell.</summary>
public interface IRemoteScreenController
{
    void RefreshScreen();
}
