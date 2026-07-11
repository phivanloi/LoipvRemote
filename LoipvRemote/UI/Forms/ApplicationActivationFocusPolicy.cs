using System;
using LoipvRemote.App;

namespace LoipvRemote.UI.Forms
{
    internal static class ApplicationActivationFocusPolicy
    {
        internal static bool ShouldRestoreActiveConnectionFocus(int message, IntPtr wParam)
        {
            return message == NativeMethods.WM_ACTIVATEAPP && wParam != IntPtr.Zero;
        }
    }
}
