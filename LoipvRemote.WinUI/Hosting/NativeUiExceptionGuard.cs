using System.Runtime.InteropServices;

namespace LoipvRemote.WinUI.Hosting;

/// <summary>
/// Keeps recoverable native/COM failures from escaping a WinUI layout callback.
/// Throwing from SizeChanged, LayoutUpdated, or DispatcherQueue callbacks can
/// terminate the XAML process before an RDP control completes its first layout.
/// </summary>
internal static class NativeUiExceptionGuard
{
    public static bool TryRun(Action action, Action<Exception> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(reportFailure);

        try
        {
            action();
            return true;
        }
        catch (COMException exception)
        {
            reportFailure(exception);
            return false;
        }
        catch (InvalidOperationException exception)
        {
            reportFailure(exception);
            return false;
        }
    }
}
