using System.Threading;
using System.Windows.Forms;
using LoipvRemote.Connection;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.UI.Adapters;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Adapters;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class ProtocolSessionBridgeEventTests
{
    [Test]
    public async Task DoesNotReportAnAsynchronousSessionAsConnectedUntilItsRuntimeConfirmsIt()
    {
        using Panel parent = new() { Size = new System.Drawing.Size(800, 600) };
        var session = new EventSession();
        await using var bridge = CreateBridge(session);
        using InterfaceControl surface = new(parent, bridge, new ConnectionInfo());
        bridge.InterfaceControl = surface;
        int connectedEvents = 0;
        bridge.Connected += _ => connectedEvents++;

        Assert.That(await bridge.InitializeAsync(), Is.True);
        Assert.That(await bridge.ConnectAsync(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(bridge.State, Is.EqualTo(ProtocolSessionState.Initialized));
            Assert.That(connectedEvents, Is.Zero);
        });

        session.RaiseConnected();

        Assert.Multiple(() =>
        {
            Assert.That(bridge.State, Is.EqualTo(ProtocolSessionState.Connected));
            Assert.That(connectedEvents, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ForwardsTheRuntimeDisconnectReasonAndErrorCode()
    {
        using Panel parent = new() { Size = new System.Drawing.Size(800, 600) };
        var session = new EventSession();
        await using var bridge = CreateBridge(session);
        using InterfaceControl surface = new(parent, bridge, new ConnectionInfo());
        bridge.InterfaceControl = surface;
        string? disconnectMessage = null;
        int? disconnectCode = null;
        int? errorCode = null;
        bridge.Disconnected += (_, message, code) =>
        {
            disconnectMessage = message;
            disconnectCode = code;
        };
        bridge.ErrorOccured += (_, _, code) => errorCode = code;

        await bridge.InitializeAsync();
        await bridge.ConnectAsync();
        session.RaiseError("RDP failed", 12);
        session.RaiseDisconnected("RDP closed", 34);

        Assert.Multiple(() =>
        {
            Assert.That(errorCode, Is.EqualTo(12));
            Assert.That(disconnectMessage, Is.EqualTo("RDP closed"));
            Assert.That(disconnectCode, Is.EqualTo(34));
            Assert.That(bridge.State, Is.EqualTo(ProtocolSessionState.Closed));
        });
    }

    [Test]
    public async Task UsesAsyncProtocolLifecycleForApplicationSessionStartup()
    {
        using Panel parent = new() { Size = new System.Drawing.Size(800, 600) };
        var session = new EventSession();
        await using var bridge = CreateBridge(session);
        using InterfaceControl surface = new(parent, bridge, new ConnectionInfo());
        bridge.InterfaceControl = surface;

        Assert.That(await bridge.InitializeAsync(), Is.True);
        Assert.That(await bridge.ConnectAsync(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(session.InitializeAsyncCalls, Is.EqualTo(1));
            Assert.That(session.ConnectAsyncCalls, Is.EqualTo(1));
        });
    }

    private static ProtocolSessionBridge CreateBridge(IProtocolSession session) => new(
        new ConnectionDefinition(
            Guid.NewGuid(), "RDP", "rdp.example", 3389, ProtocolKind.Rdp, CredentialReference.None),
        session);

    private sealed class EventSession : IProtocolSession, IProtocolSessionEvents
    {
        public int InitializeAsyncCalls { get; private set; }
        public int ConnectAsyncCalls { get; private set; }
        public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;
        public ProtocolCapabilities Capabilities => ProtocolCapabilities.None;

        public event EventHandler? Connecting;
        public event EventHandler? Connected;
        public event EventHandler<ProtocolSessionDisconnectedEventArgs>? Disconnected;
        public event EventHandler<ProtocolSessionErrorEventArgs>? ErrorOccurred;

        public bool Initialize()
        {
            State = ProtocolSessionState.Initialized;
            return true;
        }

        public bool Connect()
        {
            Connecting?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void Disconnect() => State = ProtocolSessionState.Closing;
        public void Focus() { }
        public void Close() => State = ProtocolSessionState.Closed;
        public void Dispose() { }
        public ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeAsyncCalls++;
            return ValueTask.FromResult(Initialize());
        }

        public ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            ConnectAsyncCalls++;
            return ValueTask.FromResult(Connect());
        }
        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) { Disconnect(); return ValueTask.CompletedTask; }
        public ValueTask CloseAsync(CancellationToken cancellationToken = default) { Close(); return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }

        public void RaiseConnected()
        {
            State = ProtocolSessionState.Connected;
            Connected?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseDisconnected(string message, int code)
        {
            State = ProtocolSessionState.Closed;
            Disconnected?.Invoke(this, new ProtocolSessionDisconnectedEventArgs(message, code));
        }

        public void RaiseError(string message, int code)
        {
            State = ProtocolSessionState.Faulted;
            ErrorOccurred?.Invoke(this, new ProtocolSessionErrorEventArgs(message, code));
        }
    }
}
