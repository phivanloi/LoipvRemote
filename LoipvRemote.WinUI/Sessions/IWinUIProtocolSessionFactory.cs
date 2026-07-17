using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.WinUI.Sessions;

public interface IWinUIProtocolSessionFactory
{
    IProtocolSession Create(ConnectionDefinition definition);
}
