namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Reports when a protocol-owned modal window must retain keyboard focus.</summary>
public interface IEmbeddedWindowFocusDeferral
{
    bool IsFocusBlocked { get; }
}
