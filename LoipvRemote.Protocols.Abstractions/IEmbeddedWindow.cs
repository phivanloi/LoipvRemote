namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Platform-neutral boundary for a protocol surface embedded by the desktop shell.</summary>
public interface IEmbeddedWindow
{
    bool IsAvailable { get; }
    IntPtr WindowHandle => IntPtr.Zero;
    void Focus();

    /// <summary>Focuses the embedded window while preserving the shell input queue.</summary>
    void Focus(IntPtr ownerWindowHandle) => Focus();

    bool AttachTo(IntPtr parentWindowHandle, TimeSpan timeout) => false;

    void Resize(EmbeddedWindowBounds bounds)
    {
    }
}
