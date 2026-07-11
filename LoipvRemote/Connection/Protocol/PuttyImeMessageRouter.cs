using LoipvRemote.App;

namespace LoipvRemote.Connection.Protocol
{
    internal static class PuttyImeMessageRouter
    {
        internal static bool ShouldForward(int message)
        {
            return message is NativeMethods.WM_INPUTLANGCHANGE or
                NativeMethods.WM_IME_STARTCOMPOSITION or
                NativeMethods.WM_IME_ENDCOMPOSITION or
                NativeMethods.WM_IME_COMPOSITION or
                NativeMethods.WM_IME_CHAR or
                NativeMethods.WM_IME_KEYDOWN or
                NativeMethods.WM_IME_KEYUP;
        }
    }
}
