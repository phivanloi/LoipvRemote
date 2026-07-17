using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Vnc;

/// <summary>Minimal VNC client surface required by the protocol lifecycle.</summary>
public interface IVncClient : IEmbeddedWindow
{
    bool IEmbeddedWindow.IsAvailable => true;

    void IEmbeddedWindow.Focus()
    {
    }

    void SetPort(int port);
    void Connect(string host, bool viewOnly, bool smartSize);
    void Disconnect();

    /// <summary>Supplies a password only to a client implementation that supports it.</summary>
    void SetPasswordProvider(Func<string>? passwordProvider)
    {
    }

    /// <summary>Requests a complete screen refresh when supported by the client.</summary>
    void RefreshScreen()
    {
    }

    /// <summary>Sends a remote special-key sequence when supported by the client.</summary>
    void SendSpecialKeys(VncSpecialKeys keys)
    {
    }
}
