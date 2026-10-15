namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Supplies clipboard-change notifications to protocol sessions.</summary>
public interface IClipboardChangedSource
{
    event Action? ClipboardChanged;
}
