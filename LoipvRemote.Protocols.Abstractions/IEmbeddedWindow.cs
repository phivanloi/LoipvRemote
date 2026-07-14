namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Platform-neutral boundary for a protocol surface embedded by the desktop shell.</summary>
public interface IEmbeddedWindow
{
    bool IsAvailable { get; }
    void Focus();
}
