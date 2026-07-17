using LoipvRemote.Domain.Connections;

namespace LoipvRemote.Application.Credentials;

/// <summary>Resolves a connection option secret at runtime without storing plaintext in Domain.</summary>
public interface IConnectionSecretResolver
{
    string? Resolve(ConnectionDefinition definition, string propertyName);
}
