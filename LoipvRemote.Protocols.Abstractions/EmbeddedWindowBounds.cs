namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Bounds requested by the desktop shell for a protocol session window.</summary>
public readonly record struct EmbeddedWindowBounds(int X, int Y, int Width, int Height)
{
    public bool IsValid => Width > 0 && Height > 0;
}
