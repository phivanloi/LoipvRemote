using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Vnc;
using NUnit.Framework;

namespace LoipvRemoteTests.Protocols.Vnc;

public sealed class VncProtocolFactoryTests
{
    [Test]
    public async Task CreatesVncSessionWithOptionsFromDomainBag()
    {
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "vnc", "server.example", 5901, ProtocolKind.Vnc, CredentialReference.None,
            Options: new ConnectionNodeOptions(
                new Dictionary<string, string> { ["ViewOnly"] = "true", ["SmartSize"] = "false" },
                Array.Empty<string>()));
        var client = new FakeVncClient();
        var probe = new FakeEndpointProbe();

        using IProtocolSession session = new VncProtocolFactory(() => client, () => probe).Create(definition);

        Assert.That(await session.InitializeAsync(), Is.True);
        Assert.That(await session.ConnectAsync(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(client.Port, Is.EqualTo(5901));
            Assert.That(client.ViewOnly, Is.True);
            Assert.That(client.SmartSize, Is.False);
            Assert.That(client.Host, Is.EqualTo("server.example"));
        });
    }

    [Test]
    public void RejectsNonVncProtocol()
    {
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "rdp", "server.example", 3389, ProtocolKind.Rdp, CredentialReference.None);

        Assert.That(
            () => new VncProtocolFactory(() => new FakeVncClient(), () => new FakeEndpointProbe()).Create(definition),
            Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public async Task CreatesArdSessionThroughVncTransport()
    {
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "ard", "mac.example", 5900, ProtocolKind.Ard, CredentialReference.None);
        var client = new FakeVncClient();

        using IProtocolSession session = new VncProtocolFactory(() => client, () => new FakeEndpointProbe()).Create(definition);

        Assert.That(await session.InitializeAsync(), Is.True);
        Assert.That(await session.ConnectAsync(), Is.True);
        Assert.That(client.Host, Is.EqualTo("mac.example"));
        Assert.That(client.Port, Is.EqualTo(5900));
    }

    [Test]
    public void ResolvesPasswordOnlyAtProtocolBoundary()
    {
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "vnc", "server.example", 5901, ProtocolKind.Vnc, CredentialReference.None);

        using IProtocolSession session = new VncProtocolFactory(
            () => new FakeVncClient(),
            () => new FakeEndpointProbe(),
            passwordResolver: _ => "runtime-secret")
            .Create(definition);

        Assert.That(((VncProtocolSession)session).Options.Password, Is.EqualTo("runtime-secret"));
    }

    private sealed class FakeVncClient : IVncClient
    {
        public int Port { get; private set; }
        public string? Host { get; private set; }
        public bool ViewOnly { get; private set; }
        public bool SmartSize { get; private set; }
        public void SetPort(int port) => Port = port;
        public void Connect(string host, bool viewOnly, bool smartSize)
        {
            Host = host;
            ViewOnly = viewOnly;
            SmartSize = smartSize;
        }
        public void Disconnect() { }
    }

    private sealed class FakeEndpointProbe : IVncEndpointProbe
    {
        public Task ProbeAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
