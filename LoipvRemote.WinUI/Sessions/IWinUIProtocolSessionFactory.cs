using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Putty;

namespace LoipvRemote.WinUI.Sessions;

public interface IWinUIProtocolSessionFactory
{
    IProtocolSession Create(ConnectionDefinition definition);
    ISshResourceMonitor? CreateSshResourceMonitor(ConnectionDefinition definition);
}
