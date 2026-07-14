using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Vnc;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

public sealed class VncSessionTests
{
    [Test]
    public void ConnectsThroughTheModuleOwnedLifecycle()
    {
        var client = new FakeVncClient();
        var probe = new FakeEndpointProbe();
        var session = new VncSession(client, probe);

        Assert.That(session.Initialize(new VncConnectionOptions("host.example", 5900, ViewOnly: true, SmartSize: false)), Is.True);
        Assert.That(session.Connect(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(client.Port, Is.EqualTo(5900));
            Assert.That(client.Host, Is.EqualTo("host.example"));
            Assert.That(client.ViewOnly, Is.True);
            Assert.That(client.SmartSize, Is.False);
            Assert.That(session.State, Is.EqualTo(ProtocolSessionState.Connected));
        });
    }

    [Test]
    public void DoesNotConnectBeforeInitialization()
    {
        var session = new VncSession(new FakeVncClient(), new FakeEndpointProbe());

        Assert.That(session.Connect(), Is.False);
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
