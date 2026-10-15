namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Optional PuTTY settings command exposed to the desktop shell.</summary>
public interface IPuttySettingsSession
{
    void ShowSettingsDialog();
}
