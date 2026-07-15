using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Desktop.Composition;

/// <summary>
/// Routes every supported Domain protocol to the module that owns it. Unsupported
/// kinds fail explicitly; no legacy fallback is allowed at this boundary.
/// </summary>
public sealed class ProtocolFactoryRouter(
    IProtocolFactory externalApplicationFactory,
    IProtocolFactory browserFactory,
    IProtocolFactory rdpFactory,
    IProtocolFactory vncFactory,
    IProtocolFactory puttyFactory,
    IProtocolFactory localFactory) : IProtocolFactory
{
    private readonly IProtocolFactory _externalApplicationFactory =
        externalApplicationFactory ?? throw new ArgumentNullException(nameof(externalApplicationFactory));
    private readonly IProtocolFactory _browserFactory =
        browserFactory ?? throw new ArgumentNullException(nameof(browserFactory));
    private readonly IProtocolFactory _rdpFactory =
        rdpFactory ?? throw new ArgumentNullException(nameof(rdpFactory));
    private readonly IProtocolFactory _vncFactory =
        vncFactory ?? throw new ArgumentNullException(nameof(vncFactory));
    private readonly IProtocolFactory _puttyFactory =
        puttyFactory ?? throw new ArgumentNullException(nameof(puttyFactory));
    private readonly IProtocolFactory _localFactory =
        localFactory ?? throw new ArgumentNullException(nameof(localFactory));

    public IProtocolSession Create(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.Protocol switch
        {
            ProtocolKind.ExternalApplication => _externalApplicationFactory.Create(definition),
            ProtocolKind.Http or ProtocolKind.Https or ProtocolKind.Browser => _browserFactory.Create(definition),
            ProtocolKind.Rdp => _rdpFactory.Create(definition),
            ProtocolKind.Vnc or ProtocolKind.Ard => _vncFactory.Create(definition),
            ProtocolKind.Ssh1 or ProtocolKind.Ssh2 or ProtocolKind.Telnet or ProtocolKind.Rlogin or ProtocolKind.Raw =>
                _puttyFactory.Create(definition),
            ProtocolKind.PowerShell or ProtocolKind.Terminal or ProtocolKind.Wsl or ProtocolKind.AnyDesk =>
                _localFactory.Create(definition),
            _ => throw new NotSupportedException(
                $"Protocol '{definition.Protocol}' has no registered protocol module.")
        };
    }
}
