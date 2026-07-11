using LoipvRemote.App;

namespace LoipvRemote.UI.Forms
{
    internal static class WindowTaskbarStyle
    {
        internal static int AddStandardTaskbarCommands(int windowStyle)
        {
            return windowStyle |
                   NativeMethods.WS_SYSMENU |
                   NativeMethods.WS_MINIMIZEBOX |
                   NativeMethods.WS_MAXIMIZEBOX;
        }
    }
}
