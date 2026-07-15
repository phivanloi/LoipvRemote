using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Rdp;
using LoipvRemote.Protocols.Abstractions;
using NUnit.Framework;
using System.Runtime.InteropServices;

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

    [Test]
    public void DisconnectTreatsComFailureAsClosedDuringControlShutdown()
    {
        var client = new FakeRdpClient { ThrowComExceptionOnDisconnect = true };
        var session = new RdpSession(client);

        session.Initialize(new RdpConnectionOptions("rdp.example", 3389));
        session.Connect();

        Assert.DoesNotThrow(session.Disconnect);
        Assert.That(client.DisconnectCalls, Is.EqualTo(1));
        Assert.That(session.State, Is.EqualTo(ProtocolSessionState.Closed));
    }

    private sealed class FakeRdpClient : IRdpClient
    {
        public int ConnectCalls { get; private set; }
        public int DisconnectCalls { get; private set; }
        public bool ThrowComExceptionOnDisconnect { get; init; }
        public int InitializeCalls { get; private set; }
        public string? Host { get; private set; }
        public int Port { get; private set; }

        public void Initialize() => InitializeCalls++;

        public void ConfigureEndpoint(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public void Connect() => ConnectCalls++;

        public void Disconnect()
        {
            DisconnectCalls++;
            if (ThrowComExceptionOnDisconnect)
                throw new COMException("RDP control is already disconnected.");
        }
    }
}
