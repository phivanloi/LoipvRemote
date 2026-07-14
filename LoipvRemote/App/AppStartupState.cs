using System;
using System.Diagnostics;
using System.Threading;
using LoipvRemote.UI.Forms;

namespace LoipvRemote.App;

/// <summary>Process-wide startup state shared by the Desktop entry point and UI callbacks.</summary>
public static class AppStartupState
{
    internal static Mutex? SingleInstanceMutex { get; set; }
    internal static Thread? SplashThread { get; set; }
    internal static FrmSplashScreenNew? Splash { get; set; }

    public static void CloseSingletonInstanceMutex() => SingleInstanceMutex?.Close();

    public static void CloseSplash()
    {
        FrmSplashScreenNew? splash = Splash;
        Thread? splashThread = SplashThread;
        Splash = null;
        SplashThread = null;

        if (splash is not null)
        {
            try
            {
                splash.Dispatcher.Invoke(() =>
                {
                    splash.Close();
                    splash.Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Normal);
                });
            }
            catch (Exception exception)
            {
                Trace.TraceWarning($"Failed to close splash screen: {exception}");
            }
        }

        splashThread?.Join(TimeSpan.FromSeconds(2));
    }
}
