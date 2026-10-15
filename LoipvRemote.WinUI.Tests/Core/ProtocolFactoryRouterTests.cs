using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Protocols.Abstractions;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Core;

public sealed class ProtocolFactoryRouterTests
{
    [TestCase(ProtocolKind.Rdp, 0)]
    [TestCase(ProtocolKind.Vnc, 1)]
    [TestCase(ProtocolKind.Ssh2, 2)]
    public void RoutesEveryProtocolFamilyToItsDedicatedModule(ProtocolKind protocol, int expectedFactory)
    {
        IProtocolFactory[] factories = Enumerable.Range(0, 3)
            .Select(_ => Substitute.For<IProtocolFactory>())
            .ToArray();
        var router = new ProtocolFactoryRouter(
            factories[0], factories[1], factories[2]);
        ConnectionDefinition definition = new(
            Guid.NewGuid(), "connection", "server.example", 22, protocol, CredentialReference.None);

        _ = router.Create(definition);

        factories[expectedFactory].Received(1).Create(definition);
        for (int index = 0; index < factories.Length; index++)
        {
            if (index != expectedFactory)
                factories[index].DidNotReceive().Create(Arg.Any<ConnectionDefinition>());
        }
    }

    [Test]
    public void RejectsUnknownProtocolKinds()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        var router = new ProtocolFactoryRouter(factory, factory, factory);
        ConnectionDefinition definition = new(
            Guid.NewGuid(), "unknown", "server.example", 0, (ProtocolKind)int.MaxValue, CredentialReference.None);

        Assert.That(() => router.Create(definition), Throws.TypeOf<NotSupportedException>());
    }
}
