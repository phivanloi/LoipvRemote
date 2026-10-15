namespace LoipvRemote.Protocols.Abstractions;

public interface ISmartSizingSession
{
    bool SmartSize { get; set; }
    void ToggleSmartSize();
}
