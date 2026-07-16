using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Putty;

/// <summary>Calculates embedded PuTTY terminal layout without accessing Win32 APIs.</summary>
public static class PuttyEmbeddedWindowLayout
{
    private const int WsCaption = 0x00C00000;
    private const int WsThickFrame = 0x00040000;
    private const int WsSystemMenu = 0x00080000;
    private const int WsMinimizeBox = 0x00020000;
    private const int WsMaximizeBox = 0x00010000;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsChild = 0x40000000;

    public static int CreateBorderlessChildStyle(int style)
    {
        const int nonClientChrome = WsCaption |
                                   WsThickFrame |
                                   WsSystemMenu |
                                   WsMinimizeBox |
                                   WsMaximizeBox |
                                   WsPopup;

        return (style & ~nonClientChrome) | WsChild;
    }

}
