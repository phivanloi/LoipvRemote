using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Vnc;

/// <summary>Creates VNC sessions from Domain connection definitions.</summary>
public sealed class VncProtocolFactory(
    Func<IVncClient> clientFactory,
    Func<IVncEndpointProbe> endpointProbeFactory,
    Func<IEmbeddedWindowOperations>? windowOperationsFactory = null,
    Func<ConnectionDefinition, string?>? passwordResolver = null) : IProtocolFactory
{
    private readonly Func<IVncClient> _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    private readonly Func<IVncEndpointProbe> _endpointProbeFactory = endpointProbeFactory ?? throw new ArgumentNullException(nameof(endpointProbeFactory));
    private readonly Func<IEmbeddedWindowOperations>? _windowOperationsFactory = windowOperationsFactory;
    private readonly Func<ConnectionDefinition, string?>? _passwordResolver = passwordResolver;

    public IProtocolSession Create(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Protocol != ProtocolKind.Vnc)
            throw new NotSupportedException($"Protocol '{definition.Protocol}' is not handled by {nameof(VncProtocolFactory)}.");

        bool viewOnly = ReadBoolean(definition.Options, "ViewOnly");
        bool smartSize = ReadBoolean(definition.Options, "SmartSize", defaultValue: true);
        var options = new VncConnectionOptions(
            definition.Host,
            definition.Port,
            viewOnly,
            smartSize,
            _passwordResolver?.Invoke(definition));
        return new VncProtocolSession(_clientFactory(), _endpointProbeFactory(), options, _windowOperationsFactory?.Invoke());
    }

    private static bool ReadBoolean(ConnectionNodeOptions? options, string name, bool defaultValue = false)
    {
        if (options?.Values.TryGetValue(name, out string? serialized) != true)
            return defaultValue;

        if (bool.TryParse(serialized, out bool value))
            return value;

        throw new ArgumentException($"VNC option '{name}' must be a Boolean.", nameof(options));
    }
}
