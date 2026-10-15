using LoipvRemote.Domain.Connections;
using LoipvRemote.Infrastructure.Windows.Process;
using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Putty;
using LoipvRemote.Protocols.Rdp;
using LoipvRemote.Protocols.Vnc;
using LoipvRemote.Application.Credentials;
using LoipvRemote.Infrastructure.Windows.Monitoring;
using Microsoft.UI.Dispatching;

namespace LoipvRemote.WinUI.Sessions;

/// <summary>Creates native protocol sessions for the WinUI shell.</summary>
public sealed class WinUIProtocolSessionFactory : IWinUIProtocolSessionFactory
{
    private readonly RdpProtocolFactory _rdpFactory;
    private readonly PuttyProtocolFactory _puttyFactory;
    private readonly VncProtocolFactory _vncFactory;
    private readonly SshResourceMonitorFactory _sshResourceMonitorFactory;
    private readonly RdpResourceMonitorFactory _rdpResourceMonitorFactory;
    private readonly ProtocolFactoryRouter _router;
    private readonly DispatcherQueue _dispatcher;
    private readonly LocalPowerShellRuntimeLocator _localPowerShellRuntimeLocator = new();

    public WinUIProtocolSessionFactory(
        IConnectionSecretResolver secretResolver,
        IVncClientFactory vncClientFactory,
        IRdpClientFactory rdpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(secretResolver);
        ArgumentNullException.ThrowIfNull(vncClientFactory);
        ArgumentNullException.ThrowIfNull(rdpClientFactory);
        _dispatcher = DispatcherQueue.GetForCurrentThread() ??
            throw new InvalidOperationException("The protocol session factory must be created on the WinUI thread.");

        _rdpFactory = new RdpProtocolFactory(
            rdpClientFactory.Create,
            () => new WindowsEmbeddedWindowOperations(),
            (definition, propertyName) => secretResolver.Resolve(definition, propertyName));
        _puttyFactory = new PuttyProtocolFactory(
            () => new PuttyProcessSession(WindowsJobObjectProcessTracker.AddProcess),
            () => new WindowsEmbeddedWindowOperations(),
            passwordResolver: definition => secretResolver.Resolve(definition, "Password"),
            passwordPipeFactory: WindowsSecretPipeServer.StartPassword);
        _sshResourceMonitorFactory = new SshResourceMonitorFactory(
            definition => secretResolver.Resolve(definition, "Password"));
        _rdpResourceMonitorFactory = new RdpResourceMonitorFactory(
            definition => secretResolver.Resolve(definition, "Password"));
        _vncFactory = new VncProtocolFactory(
            vncClientFactory.Create,
            () => new VncEndpointProbe(),
            () => new WindowsEmbeddedWindowOperations(),
            definition => secretResolver.Resolve(definition, "Password"));
        _router = new ProtocolFactoryRouter(
            _rdpFactory,
            _vncFactory,
            _puttyFactory);
    }

    public IProtocolSession Create(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (LocalTerminalDefinition.IsLocalTerminal(definition))
            return new LocalTerminalSession(definition, _dispatcher, _localPowerShellRuntimeLocator);

        return _router.Create(definition);
    }

    public IRemoteResourceMonitor? CreateResourceMonitor(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (LocalTerminalDefinition.IsLocalTerminal(definition))
            return new LocalResourceMonitor();

        return definition.Protocol switch
        {
            ProtocolKind.Ssh2 => _sshResourceMonitorFactory.Create(definition),
            ProtocolKind.Rdp => _rdpResourceMonitorFactory.Create(definition),
            _ => null
        };
    }
}
