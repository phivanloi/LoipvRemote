namespace LoipvRemote.Protocols.Putty;

/// <summary>Identifies IME messages that must reach an embedded PuTTY window.</summary>
public static class PuttyImeMessageRouter
{
    private const int WmInputLanguageChange = 0x0051;
    private const int WmImeStartComposition = 0x010D;
    private const int WmImeEndComposition = 0x010E;
    private const int WmImeComposition = 0x010F;
    private const int WmImeCharacter = 0x0286;
    private const int WmImeKeyDown = 0x0290;
    private const int WmImeKeyUp = 0x0291;

    public static bool ShouldForward(int message) => message is
        WmInputLanguageChange or
        WmImeStartComposition or
        WmImeEndComposition or
        WmImeComposition or
        WmImeCharacter or
        WmImeKeyDown or
        WmImeKeyUp;
}
