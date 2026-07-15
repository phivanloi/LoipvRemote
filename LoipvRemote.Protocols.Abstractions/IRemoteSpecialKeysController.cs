namespace LoipvRemote.Protocols.Abstractions;

public enum RemoteSpecialKey
{
    CtrlAltDel,
    CtrlEsc
}

/// <summary>Optional remote special-key capability exposed to the desktop shell.</summary>
public interface IRemoteSpecialKeysController
{
    void SendSpecialKeys(RemoteSpecialKey key);
}
