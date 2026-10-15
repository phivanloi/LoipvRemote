namespace LoipvRemote.Protocols.Abstractions;

public interface IViewOnlySession
{
    bool ViewOnly { get; set; }
    void ToggleViewOnly();
}
