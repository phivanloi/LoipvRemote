namespace LoipvRemote.Protocols.Abstractions;

public interface IFullscreenSession
{
    bool Fullscreen { get; set; }
    void ToggleFullscreen();
}
