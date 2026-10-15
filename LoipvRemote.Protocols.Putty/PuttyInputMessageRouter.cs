namespace LoipvRemote.Protocols.Putty;

/// <summary>
/// Identifies keyboard and input-language messages that must reach an
/// embedded PuTTY window when the desktop shell temporarily owns the focus during a
/// tab switch.
/// </summary>
public static class PuttyInputMessageRouter
{
    private const int WmInputLanguageChange = 0x0051;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmCharacter = 0x0102;
    private const int WmDeadCharacter = 0x0103;
    private const int WmSystemKeyDown = 0x0104;
    private const int WmSystemKeyUp = 0x0105;
    private const int WmSystemCharacter = 0x0106;
    private const int WmSystemDeadCharacter = 0x0107;
    private const int WmImeStartComposition = 0x010D;
    private const int WmImeEndComposition = 0x010E;
    private const int WmImeComposition = 0x010F;
    private const int WmImeCharacter = 0x0286;
    private const int WmImeKeyDown = 0x0290;
    private const int WmImeKeyUp = 0x0291;

    public static bool ShouldForward(int message) => message is
        WmInputLanguageChange or
        WmKeyDown or
        WmKeyUp or
        WmCharacter or
        WmDeadCharacter or
        WmSystemKeyDown or
        WmSystemKeyUp or
        WmSystemCharacter or
        WmSystemDeadCharacter or
        WmImeStartComposition or
        WmImeEndComposition or
        WmImeComposition or
        WmImeCharacter or
        WmImeKeyDown or
        WmImeKeyUp;
}
