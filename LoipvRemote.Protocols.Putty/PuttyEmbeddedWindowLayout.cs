using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Putty;

/// <summary>Calculates embedded PuTTY terminal layout without accessing Win32 APIs.</summary>
public static class PuttyEmbeddedWindowLayout
{
    // PuTTY may restore a non-client caption after being reparented. The
    // native host clips its children, so crop this strip above the viewport
    // while preserving the bottom edge of the terminal.
    private const int CroppedTitleBarHeight = 32;
    private const int WsCaption = 0x00C00000;
    private const int WsThickFrame = 0x00040000;
    private const int WsSystemMenu = 0x00080000;
    private const int WsMinimizeBox = 0x00020000;
    private const int WsMaximizeBox = 0x00010000;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsChild = 0x40000000;
    private const int WsExDlgModalFrame = 0x00000001;
    private const int WsExWindowEdge = 0x00000100;
    private const int WsExClientEdge = 0x00000200;
    private const int WsExStaticEdge = 0x00020000;

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

    public static int CreateBorderlessChildExtendedStyle(int style) =>
        style & ~(WsExDlgModalFrame | WsExWindowEdge | WsExClientEdge | WsExStaticEdge);

    public static EmbeddedWindowBounds CreateViewportBounds(EmbeddedWindowBounds hostBounds) =>
        new(
            hostBounds.X,
            hostBounds.Y - CroppedTitleBarHeight,
            hostBounds.Width,
            hostBounds.Height + CroppedTitleBarHeight);

}
