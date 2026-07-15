namespace LoipvRemote.Protocols.Abstractions;

/// <summary>
/// Message identifiers used by protocol sessions that expose optional input
/// forwarding. Keeping the identifiers at the protocol boundary prevents UI
/// controls from depending on Win32 interop details.
/// </summary>
public static class InputWindowMessages
{
    public const int KeyDown = 0x0100;
    public const int Character = 0x0102;
}
