using LoipvRemote.Desktop.Composition;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Protocols.Abstractions;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.Desktop.Composition;

public sealed class ProtocolFactoryRouterTests
{
    [TestCase(ProtocolKind.Http)]
    [TestCase(ProtocolKind.Https)]
    [TestCase(ProtocolKind.Browser)]
    public void RoutesBrowserKindsToBrowserFactory(ProtocolKind protocol)
    {
        IProtocolFactory external = Substitute.For<IProtocolFactory>();
        IProtocolFactory browser = Substitute.For<IProtocolFactory>();
        IProtocolFactory rdp = Substitute.For<IProtocolFactory>();
        IProtocolFactory vnc = Substitute.For<IProtocolFactory>();
        IProtocolFactory putty = Substitute.For<IProtocolFactory>();
        IProtocolFactory local = Substitute.For<IProtocolFactory>();
        IProtocolSession expected = Substitute.For<IProtocolSession>();
        browser.Create(Arg.Any<ConnectionDefinition>()).Returns(expected);
        var router = new ProtocolFactoryRouter(external, browser, rdp, vnc, putty, local);
        var definition = Definition(protocol);

        Assert.That(router.Create(definition), Is.SameAs(expected));
        browser.Received(1).Create(definition);
        external.DidNotReceiveWithAnyArgs().Create(default!);
        local.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Test]
    public void RoutesExternalApplicationToExternalFactory()
    {
        IProtocolFactory external = Substitute.For<IProtocolFactory>();
        IProtocolFactory browser = Substitute.For<IProtocolFactory>();
        IProtocolFactory rdp = Substitute.For<IProtocolFactory>();
        IProtocolFactory vnc = Substitute.For<IProtocolFactory>();
        IProtocolFactory putty = Substitute.For<IProtocolFactory>();
        IProtocolFactory local = Substitute.For<IProtocolFactory>();
        IProtocolSession expected = Substitute.For<IProtocolSession>();
        external.Create(Arg.Any<ConnectionDefinition>()).Returns(expected);
        var router = new ProtocolFactoryRouter(external, browser, rdp, vnc, putty, local);
        var definition = Definition(ProtocolKind.ExternalApplication);

        Assert.That(router.Create(definition), Is.SameAs(expected));
        external.Received(1).Create(definition);
        browser.DidNotReceiveWithAnyArgs().Create(default!);
        local.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [TestCase(ProtocolKind.Ssh1)]
    [TestCase(ProtocolKind.Ssh2)]
    [TestCase(ProtocolKind.Telnet)]
    [TestCase(ProtocolKind.Rlogin)]
    [TestCase(ProtocolKind.Raw)]
    public void RoutesPuTTYKindsToPuTTYFactory(ProtocolKind protocol)
    {
        IProtocolFactory external = Substitute.For<IProtocolFactory>();
        IProtocolFactory browser = Substitute.For<IProtocolFactory>();
        IProtocolFactory rdp = Substitute.For<IProtocolFactory>();
        IProtocolFactory vnc = Substitute.For<IProtocolFactory>();
        IProtocolFactory putty = Substitute.For<IProtocolFactory>();
        IProtocolFactory local = Substitute.For<IProtocolFactory>();
        IProtocolSession expected = Substitute.For<IProtocolSession>();
        putty.Create(Arg.Any<ConnectionDefinition>()).Returns(expected);
        var router = new ProtocolFactoryRouter(external, browser, rdp, vnc, putty, local);
        var definition = Definition(protocol);

        Assert.That(router.Create(definition), Is.SameAs(expected));
        putty.Received(1).Create(definition);
        local.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [TestCase(ProtocolKind.PowerShell)]
    [TestCase(ProtocolKind.Terminal)]
    [TestCase(ProtocolKind.Wsl)]
    [TestCase(ProtocolKind.AnyDesk)]
    public void RoutesLocalProcessKindsToLocalFactory(ProtocolKind protocol)
    {
        IProtocolFactory external = Substitute.For<IProtocolFactory>();
        IProtocolFactory browser = Substitute.For<IProtocolFactory>();
        IProtocolFactory rdp = Substitute.For<IProtocolFactory>();
        IProtocolFactory vnc = Substitute.For<IProtocolFactory>();
        IProtocolFactory putty = Substitute.For<IProtocolFactory>();
        IProtocolFactory local = Substitute.For<IProtocolFactory>();
        IProtocolSession expected = Substitute.For<IProtocolSession>();
        local.Create(Arg.Any<ConnectionDefinition>()).Returns(expected);
        var router = new ProtocolFactoryRouter(external, browser, rdp, vnc, putty, local);
        var definition = Definition(protocol);

        Assert.That(router.Create(definition), Is.SameAs(expected));
        local.Received(1).Create(definition);
    }

    private static ConnectionDefinition Definition(ProtocolKind protocol) =>
        new(Guid.NewGuid(), "test", "host.example", 443, protocol, CredentialReference.None);
}
