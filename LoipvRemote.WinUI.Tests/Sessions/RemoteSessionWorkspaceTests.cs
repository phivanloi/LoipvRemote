using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Putty;
using LoipvRemote.WinUI.Hosting;
using LoipvRemote.WinUI.Sessions;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Sessions;

public sealed class RemoteSessionWorkspaceTests
{
    [Test]
    public async Task ConnectAsyncAttachesActiveXSessionBeforeInitialization()
    {
        var events = new List<string>();
        var session = new EmbeddedTestSession(events);
        var surface = new TestSurface(events);
        var workspace = new RemoteSessionWorkspace(new TestFactory(session));
        RemoteSessionTab tab = workspace.Open(CreateConnection(ProtocolKind.Rdp));

        await workspace.ConnectAsync(tab, surface);

        Assert.That(events, Is.EqualTo(["EnsureHost", "Visible", "Attach", "Initialize", "Attach", "Connect", "Visible", "Focus", "RestoreFocusAfterTransition"]));
        Assert.That(tab.State, Is.EqualTo(RemoteSessionTabState.Connected));
        Assert.That(tab.Session, Is.SameAs(session));
    }

    [Test]
    public async Task ConnectAsyncSetsProcessHostBeforeStartingAndAttachesAfterConnect()
    {
        var events = new List<string>();
        var session = new HostEmbeddedTestSession(events);
        var surface = new TestSurface(events);
        var workspace = new RemoteSessionWorkspace(new TestFactory(session));
        RemoteSessionTab tab = workspace.Open(CreateConnection(ProtocolKind.Ssh2));

        await workspace.ConnectAsync(tab, surface);

        Assert.That(events, Is.EqualTo(["EnsureHost", "Visible", "SetHost", "Initialize", "Connect", "Attach", "Visible", "Focus", "RestoreFocusAfterTransition"]));
        Assert.That(session.HostHandle, Is.EqualTo(surface.Handle));
        Assert.That(tab.State, Is.EqualTo(RemoteSessionTabState.Connected));
    }

    [Test]
    public async Task ConnectAsyncStartsSshResourceMonitorAndCloseAsyncDisposesIt()
    {
        var session = new HostEmbeddedTestSession([]);
        var monitor = new RecordingSshResourceMonitor();
        var workspace = new RemoteSessionWorkspace(new TestFactory(session, monitor));
        RemoteSessionTab tab = workspace.Open(CreateConnection(ProtocolKind.Ssh2));

        await workspace.ConnectAsync(tab, new TestSurface([]));

        Assert.Multiple(() =>
        {
            Assert.That(monitor.Started, Is.True);
            Assert.That(tab.ResourceMonitor, Is.SameAs(monitor));
        });

        await workspace.CloseAsync(tab);

        Assert.That(monitor.Disposed, Is.True);
    }

    [Test]
    public async Task ConnectAsyncLeavesTheNativeSurfaceHiddenForAnExternalSession()
    {
        var events = new List<string>();
        var session = new ExternalTestSession(events);
        var workspace = new RemoteSessionWorkspace(new TestFactory(session));
        RemoteSessionTab tab = workspace.Open(CreateConnection(ProtocolKind.Rdp));

        await workspace.ConnectAsync(tab, new TestSurface(events));

        Assert.That(events, Is.EqualTo(["Initialize", "Connect", "Visible", "Focus"]));
        Assert.That(tab.State, Is.EqualTo(RemoteSessionTabState.Connected));
    }

    [Test]
    public void ConnectAsyncDisposesFaultedSessionAndLeavesTabRetryable()
    {
        var events = new List<string>();
        var session = new EmbeddedTestSession(events) { ConnectResult = false };
        var workspace = new RemoteSessionWorkspace(new TestFactory(session));
        RemoteSessionTab tab = workspace.Open(CreateConnection(ProtocolKind.Rdp));

        Assert.ThrowsAsync<InvalidOperationException>(async () => await workspace.ConnectAsync(tab, new TestSurface(events)));

        Assert.That(events, Is.EqualTo(["EnsureHost", "Visible", "Attach", "Initialize", "Attach", "Connect", "Close", "DisposeAsync"]));
        Assert.That(tab.State, Is.EqualTo(RemoteSessionTabState.Faulted));
        Assert.That(tab.Session, Is.Null);
    }

    [Test]
    public async Task CloseAllAsyncClosesAndDisposesEveryConnectedSession()
    {
        var first = new EmbeddedTestSession([]);
        var second = new EmbeddedTestSession([]);
        var factory = new SequenceFactory(first, second);
        var workspace = new RemoteSessionWorkspace(factory);
        var surface = new TestSurface([]);

        RemoteSessionTab firstTab = workspace.Open(CreateConnection(ProtocolKind.Rdp));
        RemoteSessionTab secondTab = workspace.Open(CreateConnection(ProtocolKind.Ssh2));
        await workspace.ConnectAsync(firstTab, surface);
        await workspace.ConnectAsync(secondTab, surface);

        await workspace.CloseAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(first.CloseCalled, Is.True);
            Assert.That(first.DisposeAsyncCalled, Is.True);
            Assert.That(second.CloseCalled, Is.True);
            Assert.That(second.DisposeAsyncCalled, Is.True);
            Assert.That(firstTab.State, Is.EqualTo(RemoteSessionTabState.Closed));
            Assert.That(secondTab.State, Is.EqualTo(RemoteSessionTabState.Closed));
        });
    }

    [Test]
    public async Task CloseAllAsyncAttemptsEverySessionWhenOneProtocolFailsToClose()
    {
        var failing = new EmbeddedTestSession([]) { ThrowOnClose = true };
        var healthy = new EmbeddedTestSession([]);
        var workspace = new RemoteSessionWorkspace(new SequenceFactory(failing, healthy));
        var surface = new TestSurface([]);
        RemoteSessionTab failingTab = workspace.Open(CreateConnection(ProtocolKind.Rdp));
        RemoteSessionTab healthyTab = workspace.Open(CreateConnection(ProtocolKind.Ssh2));
        await workspace.ConnectAsync(failingTab, surface);
        await workspace.ConnectAsync(healthyTab, surface);

        Assert.ThrowsAsync<AggregateException>(async () => await workspace.CloseAllAsync());

        Assert.Multiple(() =>
        {
            Assert.That(failingTab.State, Is.EqualTo(RemoteSessionTabState.Closed));
            Assert.That(healthyTab.State, Is.EqualTo(RemoteSessionTabState.Closed));
            Assert.That(healthy.CloseCalled, Is.True);
            Assert.That(healthy.DisposeAsyncCalled, Is.True);
        });
    }

    [Test]
    public async Task ActivateReattachesTheSelectedSessionAndFocusesItWithoutReconnecting()
    {
        var events = new List<string>();
        var session = new EmbeddedTestSession(events);
        var surface = new TestSurface(events);
        var workspace = new RemoteSessionWorkspace(new TestFactory(session));
        RemoteSessionTab tab = workspace.Open(CreateConnection(ProtocolKind.Rdp));
        await workspace.ConnectAsync(tab, surface);

        events.Clear();
        RemoteSessionWorkspace.Activate(tab, surface);

        Assert.That(events, Is.EqualTo(["EnsureHost", "Attach", "Visible", "Focus", "RestoreFocusAfterTransition"]));
        Assert.That(tab.State, Is.EqualTo(RemoteSessionTabState.Connected));
    }

    [Test]
    public async Task ActivateReassertsVisibilityAndFocusAfterRepeatedTabSwitches()
    {
        var events = new List<string>();
        var session = new EmbeddedTestSession(events);
        var surface = new TestSurface(events);
        var workspace = new RemoteSessionWorkspace(new TestFactory(session));
        RemoteSessionTab tab = workspace.Open(CreateConnection(ProtocolKind.Ssh2));
        await workspace.ConnectAsync(tab, surface);

        events.Clear();
        RemoteSessionWorkspace.Activate(tab, surface);
        RemoteSessionWorkspace.Activate(tab, surface);

        Assert.That(events, Is.EqualTo([
            "EnsureHost", "Attach", "Visible", "Focus", "RestoreFocusAfterTransition",
            "EnsureHost", "Attach", "Visible", "Focus", "RestoreFocusAfterTransition"]));
    }

    [Test]
    public async Task CloseAsyncCancelsAnInFlightConnectionAndCleansUpItsProtocolSession()
    {
        var session = new BlockingEmbeddedTestSession();
        var workspace = new RemoteSessionWorkspace(new TestFactory(session));
        RemoteSessionTab tab = workspace.Open(CreateConnection(ProtocolKind.Ssh2));

        Task connectTask = workspace.ConnectAsync(tab, new TestSurface([]));
        await session.InitializationStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await workspace.CloseAsync(tab);

        Exception? cancellation = null;
        try
        {
            await connectTask;
        }
        catch (Exception exception)
        {
            cancellation = exception;
        }

        Assert.That(cancellation, Is.InstanceOf<OperationCanceledException>());
        Assert.Multiple(() =>
        {
            Assert.That(session.CloseCalled, Is.True);
            Assert.That(session.DisposeAsyncCalled, Is.True);
            Assert.That(tab.Session, Is.Null);
            Assert.That(tab.State, Is.EqualTo(RemoteSessionTabState.Closed));
        });
    }

    private static ConnectionDefinition CreateConnection(ProtocolKind protocol) => new(
        Guid.NewGuid(),
        "Test",
        "localhost",
        protocol == ProtocolKind.Rdp ? 3389 : 22,
        protocol,
        CredentialReference.None);

    private sealed class TestFactory(IProtocolSession session, ISshResourceMonitor? resourceMonitor = null) : IWinUIProtocolSessionFactory
    {
        public IProtocolSession Create(ConnectionDefinition definition) => session;
        public ISshResourceMonitor? CreateSshResourceMonitor(ConnectionDefinition definition) => resourceMonitor;
    }

    private sealed class SequenceFactory(params IProtocolSession[] sessions) : IWinUIProtocolSessionFactory
    {
        private readonly Queue<IProtocolSession> _sessions = new(sessions);

        public IProtocolSession Create(ConnectionDefinition definition) => _sessions.Dequeue();
        public ISshResourceMonitor? CreateSshResourceMonitor(ConnectionDefinition definition) => null;
    }

    private sealed class TestSurface(List<string> events) : IEmbeddedSessionSurface
    {
        public IntPtr Handle { get; } = new(4321);

        public void EnsureHostWindow() => events.Add("EnsureHost");

        public void SetVisible(bool visible) => events.Add("Visible");

        public bool Attach(IEmbeddedWindow session, TimeSpan timeout)
        {
            events.Add("Attach");
            return true;
        }

        public void Focus() => events.Add("Focus");

        public void RestoreFocusAfterTransition() => events.Add("RestoreFocusAfterTransition");
    }

    private class EmbeddedTestSession(List<string> events) : IProtocolSession, IEmbeddedWindow
    {
        private readonly List<string> _events = events;

        public bool ConnectResult { get; init; } = true;
        public bool ThrowOnClose { get; init; }
        public bool CloseCalled { get; private set; }
        public bool DisposeAsyncCalled { get; private set; }
        public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;
        public ProtocolCapabilities Capabilities => ProtocolCapabilities.EmbeddedWindow;
        public bool IsAvailable => true;

        public void Focus()
        {
        }

        public ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            _events.Add("Initialize");
            State = ProtocolSessionState.Initialized;
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            _events.Add("Connect");
            State = ConnectResult ? ProtocolSessionState.Connected : ProtocolSessionState.Faulted;
            return ValueTask.FromResult(ConnectResult);
        }

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken cancellationToken = default)
        {
            _events.Add("Close");
            CloseCalled = true;
            State = ProtocolSessionState.Closed;
            if (ThrowOnClose)
                throw new InvalidOperationException("Protocol close failed.");
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            _events.Add("DisposeAsync");
            DisposeAsyncCalled = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class HostEmbeddedTestSession(List<string> events) : EmbeddedTestSession(events), IEmbeddedWindowHost
    {
        private readonly List<string> _events = events;

        public IntPtr HostHandle { get; private set; }

        public void SetHostWindowHandle(IntPtr parentWindowHandle)
        {
            HostHandle = parentWindowHandle;
            _events.Add("SetHost");
        }
    }

    private sealed class ExternalTestSession(List<string> events) : IProtocolSession
    {
        public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;
        public ProtocolCapabilities Capabilities => ProtocolCapabilities.Reconnect;

        public void Focus() => events.Add("Focus");

        public ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            events.Add("Initialize");
            State = ProtocolSessionState.Initialized;
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            events.Add("Connect");
            State = ProtocolSessionState.Connected;
            return ValueTask.FromResult(true);
        }

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingEmbeddedTestSession : IProtocolSession, IEmbeddedWindow
    {
        public TaskCompletionSource InitializationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CloseCalled { get; private set; }
        public bool DisposeAsyncCalled { get; private set; }
        public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;
        public ProtocolCapabilities Capabilities => ProtocolCapabilities.EmbeddedWindow;
        public bool IsAvailable => true;

        public void Focus()
        {
        }

        public async ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            State = ProtocolSessionState.Initialized;
            InitializationStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return true;
        }

        public ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken cancellationToken = default)
        {
            CloseCalled = true;
            State = ProtocolSessionState.Closed;
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCalled = true;
            return ValueTask.CompletedTask;
        }
    }

#pragma warning disable CS0067 // Events are required by the monitor contract; this fake does not raise them.
    private sealed class RecordingSshResourceMonitor : ISshResourceMonitor
    {
        public event Action<RemoteResourceSnapshot>? SnapshotUpdated;
        public event Action<SshResourceMonitorStatus>? StatusChanged;
        public RemoteResourceSnapshot? LastSnapshot => null;
        public SshResourceMonitorStatus LastStatus { get; } = new(SshResourceMonitorState.WaitingForActiveTab, string.Empty);
        public bool Started { get; private set; }
        public bool Disposed { get; private set; }

        public void Start() => Started = true;
        public void SetIsActive(bool isActive) { }
        public void StopMonitoring() { }
        public void Dispose() => Disposed = true;
    }
#pragma warning restore CS0067
}
