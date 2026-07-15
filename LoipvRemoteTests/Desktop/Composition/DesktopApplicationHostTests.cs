using LoipvRemote.App.Composition;
using LoipvRemote.App;
using LoipvRemote.Connection;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.Desktop.Composition;
using LoipvRemote.Infrastructure.Persistence.Xml;
using LoipvRemote.Infrastructure.Windows.Registry;
using LoipvRemote.Infrastructure.Windows.ApplicationIdentity;
using LoipvRemote.Infrastructure.Windows.WindowActivation;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.ExternalApps;
using LoipvRemote.UseCases.Configuration;
using LoipvRemote.UseCases.Sessions;
using LoipvRemote.UI.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.Desktop.Composition;

public class DesktopApplicationHostTests
{
    [Test]
    public async Task CreatesOneHostOwnedCompositionRootWithCallerSuppliedProtocolFactory()
    {
        IProtocolFactory protocolFactory = Substitute.For<IProtocolFactory>();

        using IHost host = DesktopApplicationHost.Create([], services =>
            services.AddSingleton(protocolFactory));

        await host.StartAsync();

        DesktopCompositionRoot root = host.Services.GetRequiredService<DesktopCompositionRoot>();

        Assert.That(root.SessionOrchestrator, Is.Not.Null);
        Assert.That(root.SessionLifecycleCoordinator, Is.SameAs(
            host.Services.GetRequiredService<LoipvRemote.UseCases.Sessions.SessionLifecycleCoordinator>()));

        await host.StopAsync();
    }

    [Test]
    public async Task ProductionRegistrationResolvesProtocolPersistenceAndConnectorServices()
    {
        using IHost host = DesktopApplicationHost.Create([], ApplicationServiceRegistration.Register);

        await host.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(host.Services.GetRequiredService<IProtocolFactory>(), Is.Not.Null);
            Assert.That(host.Services.GetRequiredService<ExternalCredentialConnectorRegistry>(), Is.Not.Null);
            Assert.That(host.Services.GetRequiredService<IWindowsRegistryValueReader>(), Is.Not.Null);
            Assert.That(host.Services.GetRequiredService<IApplicationIdentityService>(), Is.Not.Null);
            Assert.That(host.Services.GetRequiredService<IWindowActivationService>(), Is.Not.Null);
            Assert.That(host.Services.GetRequiredService<IExternalApplicationHostFactory>(), Is.Not.Null);
            Assert.That(host.Services.GetRequiredService<IConnectionDefinitionStoreFactory>()
                .Create(new ConnectionDefinitionStoreOptions(ConnectionDefinitionStoreKind.Xml, "connections.xml")),
                Is.TypeOf<XmlConnectionDefinitionStore>());
            Assert.That(host.Services.GetRequiredService<ConnectionStoreConfigurationService>(), Is.Not.Null);
            Assert.That(host.Services.GetRequiredService<IConnectionTreeWorkspace>(), Is.TypeOf<ConnectionWorkspace>());
            Assert.That(host.Services.GetRequiredService<IConnectionTreeWorkspace>(), Is.Not.Null);
            Assert.That(host.Services.GetRequiredService<ConnectionExportService>(), Is.Not.Null);
            Assert.That(host.Services.GetRequiredService<DesktopShellRuntime>(), Is.Not.Null);
            Assert.That(host.Services.GetRequiredService<ConnectionInitiator>(), Is.Not.Null);
            Assert.That(host.Services.GetRequiredService<ConnectionWorkspaceAdapter>(), Is.Not.Null);
        });

        await host.StopAsync();
    }

    [Test]
    public async Task HostShutdownClosesAndDisposesTrackedSessions()
    {
        IProtocolFactory protocolFactory = Substitute.For<IProtocolFactory>();
        IProtocolSession session = Substitute.For<IProtocolSession>();
        session.Initialize().Returns(true);
        session.Connect().Returns(true);

        using IHost host = DesktopApplicationHost.Create([], services =>
            services.AddSingleton(protocolFactory));
        await host.StartAsync();

        SessionLifecycleCoordinator lifecycle = host.Services.GetRequiredService<SessionLifecycleCoordinator>();
        Assert.That(lifecycle.Start(session), Is.EqualTo(SessionStartResult.Started));

        await host.StopAsync();

        session.Received(1).Disconnect();
        session.Received(1).Close();
        session.Received(1).Dispose();
    }
}
