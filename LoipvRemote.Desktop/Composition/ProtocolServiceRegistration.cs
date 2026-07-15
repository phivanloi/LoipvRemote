using LoipvRemote.Infrastructure.Windows.Com;
using LoipvRemote.Infrastructure.Windows.Process;
using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using LoipvRemote.Domain.Protocols.Rdp;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Browser;
using LoipvRemote.Protocols.ExternalApps;
using LoipvRemote.Protocols.Putty;
using LoipvRemote.Protocols.Rdp;
using LoipvRemote.Protocols.Vnc;
using LoipvRemote.UseCases.Credentials;
using LoipvRemote.UseCases.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoipvRemote.Desktop.Composition;

/// <summary>Registers protocol runtime modules for the WinForms desktop host.</summary>
public static class ProtocolServiceRegistration
{
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPuttyExecutablePathProvider, SystemPuttyExecutablePathProvider>();

        services.AddSingleton<ExternalApplicationProtocolFactory>();
        services.AddSingleton<LocalProtocolFactory>();
        services.AddSingleton<BrowserProtocolFactory>(_ =>
            new BrowserProtocolFactory(
                () => new BrowserDesktopClient(useEdge: true),
                () => new WindowsEmbeddedWindowOperations()));
        services.AddSingleton<RdpProtocolFactory>(provider =>
            new RdpProtocolFactory(
                requestedVersion => new RdpActiveXRuntime(requestedVersion == RdpVersion.Highest
                    ? RdpVersionSelector.SelectHighestSupported(RdpActiveXRuntime.IsSupported)
                    : requestedVersion),
                () => new WindowsEmbeddedWindowOperations(),
                (definition, propertyName) => provider.GetRequiredService<IConnectionSecretResolver>().Resolve(definition, propertyName)));
        services.AddSingleton<VncProtocolFactory>(provider =>
            new VncProtocolFactory(
                () => new VncDesktopClient(),
                () => new VncEndpointProbe(),
                () => new WindowsEmbeddedWindowOperations(),
                definition => provider.GetRequiredService<IConnectionSecretResolver>().Resolve(definition, "Password")));
        services.AddSingleton<PuttyProtocolFactory>(provider =>
            new PuttyProtocolFactory(
                () => new PuttyProcessSession(),
                () => new WindowsEmbeddedWindowOperations(),
                () => provider.GetRequiredService<IPuttyExecutablePathProvider>().Resolve(),
                definition => provider.GetRequiredService<IConnectionSecretResolver>().Resolve(definition, "Password"),
                WindowsSecretPipeServer.StartPassword));
        services.AddSingleton<IProtocolFactory>(provider => new ProtocolFactoryRouter(
            provider.GetRequiredService<ExternalApplicationProtocolFactory>(),
            provider.GetRequiredService<BrowserProtocolFactory>(),
            provider.GetRequiredService<RdpProtocolFactory>(),
            provider.GetRequiredService<VncProtocolFactory>(),
            provider.GetRequiredService<PuttyProtocolFactory>(),
            provider.GetRequiredService<LocalProtocolFactory>()));
    }
}
