namespace LoipvRemote.Infrastructure.Windows.WindowEmbedding;

/// <summary>
/// Determines whether an embedded native window may take keyboard focus after
/// it has finished attaching to its WinForms host.
/// </summary>
public static class EmbeddedWindowActivationPolicy
{
    public static bool ShouldRequestFocus(bool hostIsDisposed, bool hostHasHandle, bool hostIsVisible)
    {
        return !hostIsDisposed && hostHasHandle && hostIsVisible;
    }
}
