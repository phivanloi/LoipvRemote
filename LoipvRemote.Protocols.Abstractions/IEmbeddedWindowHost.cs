namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Provides a native parent handle before an embedded process starts.</summary>
public interface IEmbeddedWindowHost
{
    void SetHostWindowHandle(IntPtr parentWindowHandle);
}
