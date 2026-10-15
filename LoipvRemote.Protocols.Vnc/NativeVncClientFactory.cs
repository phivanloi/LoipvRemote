namespace LoipvRemote.Protocols.Vnc;

/// <summary>Creates the native child-window renderer used by the WinUI desktop.</summary>
public sealed class NativeVncClientFactory : IVncClientFactory
{
    public IVncClient Create() => new NativeVncClient();
}
