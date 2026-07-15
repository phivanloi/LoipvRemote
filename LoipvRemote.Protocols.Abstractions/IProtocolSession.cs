using LoipvRemote.Domain.Protocols;

namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Lifecycle contract shared by every remote protocol session.</summary>
public interface IProtocolSession : IDisposable
{
    ProtocolSessionState State { get; }
    ProtocolCapabilities Capabilities { get; }

    bool Initialize();
    bool Connect();
    void Disconnect();
    void Focus();
    void Close();

}
