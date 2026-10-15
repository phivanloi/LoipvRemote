namespace LoipvRemote.Protocols.Abstractions;

/// <summary>
/// Embedded RDP session capability used by the desktop shell to prepare the
/// initial desktop and to apply debounced dynamic-resolution updates.
/// </summary>
public interface IAdaptiveRdpDisplaySession
{
    void PrepareDisplay(RdpDisplayConfiguration display);
    bool TryUpdateDisplay(RdpDisplayConfiguration display);
}
