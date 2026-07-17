using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Validation;

namespace LoipvRemote.Application.Sessions;

/// <summary>Builds an in-memory connection for Quick Connect without persisting it to a store.</summary>
public static class QuickConnectionDefinitionFactory
{
    public static ConnectionDefinition Create(
        string host,
        int port,
        ProtocolKind protocol,
        ConnectionNodeOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        var definition = new ConnectionDefinition(
            Guid.NewGuid(),
            $"Quick: {host.Trim()}",
            host.Trim(),
            port,
            protocol,
            CredentialReference.None,
            Options: options);
        ConnectionDefinitionValidator.Validate(definition);
        return definition;
    }
}
