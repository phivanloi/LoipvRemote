using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.Infrastructure.Windows.Process;
using LoipvRemote.Messages;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Resources.Language;

namespace LoipvRemote.Connection.Protocol.AnyDesk;

[SupportedOSPlatform("windows")]
public sealed class ProtocolAnyDesk : ProtocolBase
{
    private readonly ConnectionInfo _connectionInfo;
    private readonly WindowsAnyDeskSession _session = new();

    public ProtocolAnyDesk(ConnectionInfo connectionInfo)
    {
        _connectionInfo = connectionInfo;
        _session.Exited += SessionOnExited;
    }

    public override bool Connect()
    {
        try
        {
            MessageCollector?.AddMessage(MessageClass.InformationMsg,
                "Attempting to start AnyDesk connection.", true);

            string? executablePath = AnyDeskExecutableLocator.Find();
            if (string.IsNullOrEmpty(executablePath))
            {
                MessageCollector?.AddMessage(MessageClass.ErrorMsg,
                    "AnyDesk is not installed. Please install AnyDesk to use this protocol.", true);
                return false;
            }

            Tools.PathValidator.ValidateExecutablePathOrThrow(executablePath, nameof(executablePath));
            string identifier = _connectionInfo.Hostname?.Trim() ?? string.Empty;
            if (!AnyDeskLaunch.IsValidIdentifier(identifier))
            {
                MessageCollector?.AddMessage(MessageClass.ErrorMsg,
                    "Invalid AnyDesk ID format. Only alphanumeric characters, @, -, _, and . are allowed.", true);
                return false;
            }

            if (!_session.Start(executablePath, identifier, _connectionInfo.Password, TimeSpan.FromSeconds(10)))
            {
                MessageCollector?.AddMessage(MessageClass.WarningMsg,
                    "AnyDesk window did not appear within the expected time.", true);
                return false;
            }

            EmbeddedWindowOperations.SetParent(_session.WindowHandle, InterfaceControl.Handle);
            Resize(this, EventArgs.Empty);
            return base.Connect();
        }
        catch (Exception ex)
        {
            MessageCollector?.AddExceptionMessage(Language.ConnectionFailed, ex);
            return false;
        }
    }

    public override void Focus()
    {
        try
        {
            if (_session.WindowHandle != 0)
                EmbeddedWindowOperations.Activate(_session.WindowHandle);
        }
        catch (Exception ex)
        {
            MessageCollector?.AddExceptionMessage(Language.IntAppFocusFailed, ex);
        }
    }

    protected override void Resize(object sender, EventArgs e)
    {
        try
        {
            if (_session.WindowHandle == 0 || InterfaceControl.Size == Size.Empty)
                return;

            Rectangle clientRect = InterfaceControl.ClientRectangle;
            EmbeddedWindowOperations.Move(
                _session.WindowHandle,
                clientRect.X - SystemInformation.FrameBorderSize.Width,
                clientRect.Y - (SystemInformation.CaptionHeight + SystemInformation.FrameBorderSize.Height),
                clientRect.Width + SystemInformation.FrameBorderSize.Width * 2,
                clientRect.Height + SystemInformation.CaptionHeight + SystemInformation.FrameBorderSize.Height * 2);
        }
        catch (Exception ex)
        {
            MessageCollector?.AddExceptionMessage(Language.IntAppResizeFailed, ex);
        }
    }

    public override void Close()
    {
        try
        {
            if (_session.WindowHandle != 0)
                EmbeddedWindowOperations.Close(_session.WindowHandle);
            _session.Stop();
        }
        catch (Exception ex)
        {
            MessageCollector?.AddExceptionMessage(Language.IntAppKillFailed, ex);
        }

        base.Close();
    }

    private void SessionOnExited(object? sender, EventArgs e) => Event_Closed(this);

    public enum Defaults
    {
        Port = 0
    }
}
