using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.UseCases.Sessions;

/// <summary>Creates a session from an application-owned request type.</summary>
public interface IProtocolSessionFactory<in TRequest>
{
    IProtocolSession Create(TRequest request);
}
