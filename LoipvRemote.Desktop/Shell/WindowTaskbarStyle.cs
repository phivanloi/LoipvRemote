namespace LoipvRemote.Desktop.Shell;

/// <summary>Adds the standard taskbar system commands to a borderless desktop shell.</summary>
public static class WindowTaskbarStyle
{
    private const int WsSystemMenu = 0x00080000;
    private const int WsMinimizeBox = 0x00020000;
    private const int WsMaximizeBox = 0x00010000;

    public static int AddStandardTaskbarCommands(int windowStyle) =>
        windowStyle | WsSystemMenu | WsMinimizeBox | WsMaximizeBox;
}
