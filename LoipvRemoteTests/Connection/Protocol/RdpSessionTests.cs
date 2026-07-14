using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Rdp;
using LoipvRemote.Protocols.Abstractions;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

public sealed class RdpSessionTests
{
    [Test]
    public void ConnectsAndDisconnectsThroughModuleLifecycle()
    {
        var client = new FakeRdpClient();
        var session = new RdpSession(client);

        Assert.That(session.Initialize(new RdpConnectionOptions("rdp.example", 3389)), Is.True);
        Assert.That(session.Connect(), Is.True);
        session.Disconnect();

        Assert.That(client.ConnectCalls, Is.EqualTo(1));
        Assert.That(client.DisconnectCalls, Is.EqualTo(1));
        Assert.That(session.State, Is.EqualTo(ProtocolSessionState.Closed));
    }

    [Test]
    public void DoesNotConnectBeforeInitialization()
    {
        var client = new FakeRdpClient();

        Assert.That(new RdpSession(client).Connect(), Is.False);
        Assert.That(client.ConnectCalls, Is.Zero);
    }

    private sealed class FakeRdpClient : IRdpClient
    {
        public int ConnectCalls { get; private set; }
        public int DisconnectCalls { get; private set; }

        public void Connect() => ConnectCalls++;

        public void Disconnect() => DisconnectCalls++;
    }
}
