using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using LoipvRemote.UI.Forms;

namespace LoipvRemote.App;

/// <summary>Process-wide startup state shared by the Desktop entry point and UI callbacks.</summary>
public static class AppStartupState
{
    internal static Mutex? SingleInstanceMutex { get; set; }
    internal static Thread? SplashThread { get; set; }
    internal static FrmSplashScreen? Splash { get; set; }

    public static void CloseSingletonInstanceMutex() => SingleInstanceMutex?.Close();

    public static void CloseSplash()
    {
        FrmSplashScreen? splash = Splash;
        Thread? splashThread = SplashThread;
        Splash = null;
        SplashThread = null;

        if (splash is not null)
        {
            try
            {
                if (splash.IsHandleCreated)
                    splash.BeginInvoke(splash.Close);
                else
                    splash.Close();
            }
            catch (Exception exception)
            {
                Trace.TraceWarning($"Failed to close splash screen: {exception}");
            }
        }

        splashThread?.Join(TimeSpan.FromSeconds(2));
    }
}
