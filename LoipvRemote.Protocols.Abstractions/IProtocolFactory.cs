using LoipvRemote.Domain.Connections;

namespace LoipvRemote.Protocols.Abstractions;

public interface IProtocolFactory
{
    IProtocolSession Create(ConnectionDefinition definition);
}
