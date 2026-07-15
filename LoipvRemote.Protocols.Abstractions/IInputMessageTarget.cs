namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Receives input messages that must be delivered to an embedded protocol window.</summary>
public interface IInputMessageTarget
{
    bool TryForwardInputMessage(int message, IntPtr wParam, IntPtr lParam);
}
