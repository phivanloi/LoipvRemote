using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.ExternalApps;

/// <summary>
/// Creates the external-application session owned by the ExternalApps module.
/// Unsupported protocol kinds are deliberately rejected so the composition root
/// can route them to the next module without coupling this project to desktop code.
/// </summary>
public sealed class ExternalApplicationProtocolFactory(
    IExternalApplicationHostFactory hostFactory) : IProtocolFactory
{
    private readonly IExternalApplicationHostFactory _hostFactory =
        hostFactory ?? throw new ArgumentNullException(nameof(hostFactory));

    public IProtocolSession Create(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Protocol != ProtocolKind.ExternalApplication)
            throw new NotSupportedException($"Protocol '{definition.Protocol}' is not handled by {nameof(ExternalApplicationProtocolFactory)}.");

        if (definition.ExternalApplication is null)
            throw new ArgumentException("External application sessions require a command definition.", nameof(definition));

        return new ExternalApplicationSession(definition.ExternalApplication, _hostFactory.Create());
    }
}
