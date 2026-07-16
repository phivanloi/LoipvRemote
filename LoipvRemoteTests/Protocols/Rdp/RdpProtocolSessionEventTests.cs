using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Rdp;
using NUnit.Framework;

namespace LoipvRemoteTests.Protocols.Rdp;

public sealed class RdpProtocolSessionEventTests
{
    [Test]
    public async Task ReportsConnectedOnlyAfterTheActiveXControlRaisesConnected()
    {
        var client = new EventRdpClient();
        using var session = new RdpProtocolSession(
            client,
            new RdpConnectionOptions("rdp.example", 3389));
        int connectedEvents = 0;
        ((IProtocolSessionEvents)session).Connected += (_, _) => connectedEvents++;

        Assert.That(await session.InitializeAsync(), Is.True);
        Assert.That(await session.ConnectAsync(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(client.EventsSubscribed, Is.True);
            Assert.That(session.State, Is.EqualTo(ProtocolSessionState.Initialized));
            Assert.That(connectedEvents, Is.Zero);
        });

        client.RaiseConnected();

        Assert.Multiple(() =>
        {
            Assert.That(session.State, Is.EqualTo(ProtocolSessionState.Connected));
            Assert.That(connectedEvents, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ConvertsActiveXDisconnectAndFatalErrorIntoProtocolEvents()
    {
        var client = new EventRdpClient();
        using var session = new RdpProtocolSession(
            client,
            new RdpConnectionOptions("rdp.example", 3389));
        ProtocolSessionDisconnectedEventArgs? disconnected = null;
        ProtocolSessionErrorEventArgs? error = null;
        IProtocolSessionEvents events = session;
        events.Disconnected += (_, args) => disconnected = args;
        events.ErrorOccurred += (_, args) => error = args;

        await session.InitializeAsync();
        await session.ConnectAsync();
        client.RaiseFatalError(42);

        Assert.Multiple(() =>
        {
            Assert.That(error?.Code, Is.EqualTo(42));
            Assert.That(session.State, Is.EqualTo(ProtocolSessionState.Faulted));
        });

        client.RaiseDisconnected(7);

        Assert.Multiple(() =>
        {
            Assert.That(disconnected?.Code, Is.EqualTo(7));
            Assert.That(disconnected?.Message, Is.EqualTo("RDP disconnect 7"));
            Assert.That(session.State, Is.EqualTo(ProtocolSessionState.Closed));
        });
    }

    private sealed class EventRdpClient : IRdpClient, IRdpEventClient
    {
        public bool EventsSubscribed { get; private set; }

        public event EventHandler? Connecting;
        public event EventHandler? Connected;
        public event EventHandler? LoginComplete;
        public event EventHandler<int>? FatalError;
        public event EventHandler<int>? Disconnected;
        public event EventHandler? IdleTimeout;
        public event EventHandler? LeaveFullScreen;

        public void Initialize() { }
        public void ConfigureEndpoint(string host, int port) { }
        public void Connect() => Connecting?.Invoke(this, EventArgs.Empty);
        public void Disconnect() { }
        public string GetErrorDescription(int disconnectReason) => $"RDP disconnect {disconnectReason}";
        public void SubscribeEvents() => EventsSubscribed = true;
        public void UnsubscribeEvents() => EventsSubscribed = false;

        public void RaiseConnected() => Connected?.Invoke(this, EventArgs.Empty);
        public void RaiseFatalError(int code) => FatalError?.Invoke(this, code);
        public void RaiseDisconnected(int code) => Disconnected?.Invoke(this, code);
    }
}
