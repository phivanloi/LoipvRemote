namespace LoipvRemote.Protocols.Abstractions;

/// <summary>
/// Exposes a verifiable focus transfer for cross-process embedded windows.
/// The shell uses the result to retry focus after tab and window transitions.
/// </summary>
public interface IEmbeddedWindowFocusTarget
{
    bool TryFocus(IntPtr ownerWindowHandle);
}
