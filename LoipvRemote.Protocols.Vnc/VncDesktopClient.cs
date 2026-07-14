using System.Windows.Forms;
using VncSharpCore;

namespace LoipvRemote.Protocols.Vnc;

public sealed class VncDesktopClient
{
    private readonly RemoteDesktop _control = new();

    public VncDesktopClient()
    {
        Session = new VncSharpClientAdapter(_control);
        _control.ConnectComplete += (_, args) => Connected?.Invoke(this, args);
        _control.ConnectionLost += (_, args) => Disconnected?.Invoke(this, args);
    }

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;

    public Control Control => _control;
    public IVncClient Session { get; }

    public Func<string>? PasswordProvider
    {
        set => _control.GetPassword = value is null ? null : new AuthenticateDelegate(value);
    }

    public bool AutoScroll
    {
        set => _control.AutoScroll = value;
    }

    public void SendSpecialKeys(VncSpecialKeys keys) => _control.SendSpecialKeys(keys switch
    {
        VncSpecialKeys.CtrlAltDel => SpecialKeys.CtrlAltDel,
        VncSpecialKeys.CtrlEsc => SpecialKeys.CtrlEsc,
        _ => throw new ArgumentOutOfRangeException(nameof(keys), keys, null)
    });

    public void RefreshScreen() => _control.FullScreenUpdate();

    public void FillServerClipboard() => _control.FillServerClipboard();
}

public enum VncSpecialKeys
{
    CtrlAltDel,
    CtrlEsc
}
