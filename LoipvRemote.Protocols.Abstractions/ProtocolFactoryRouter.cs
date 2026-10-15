using LoipvRemote.Domain.Connections;

namespace LoipvRemote.Protocols.Abstractions;

/// <summary>
/// Routes each Domain protocol kind to the independently-owned protocol module.
/// This is a host-neutral composition primitive: WinUI and test hosts use the
/// same routing policy without referencing a desktop shell.
/// </summary>
public sealed class ProtocolFactoryRouter(
    IProtocolFactory rdpFactory,
    IProtocolFactory vncFactory,
    IProtocolFactory puttyFactory) : IProtocolFactory
{
    private readonly IProtocolFactory _rdpFactory =
        rdpFactory ?? throw new ArgumentNullException(nameof(rdpFactory));
    private readonly IProtocolFactory _vncFactory =
        vncFactory ?? throw new ArgumentNullException(nameof(vncFactory));
    private readonly IProtocolFactory _puttyFactory =
        puttyFactory ?? throw new ArgumentNullException(nameof(puttyFactory));

    public IProtocolSession Create(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.Protocol switch
        {
            ProtocolKind.Rdp => _rdpFactory.Create(definition),
            ProtocolKind.Vnc => _vncFactory.Create(definition),
            ProtocolKind.Ssh2 => _puttyFactory.Create(definition),
            _ => throw new NotSupportedException($"Protocol '{definition.Protocol}' has no registered protocol module.")
        };
    }
}
