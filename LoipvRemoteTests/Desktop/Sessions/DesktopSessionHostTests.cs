using System.Drawing;
using LoipvRemote.Desktop.Sessions;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;
using NUnit.Framework;

namespace LoipvRemoteTests.Desktop.Sessions;

[TestFixture]
public sealed class DesktopSessionHostTests
{
    [Test]
    public async Task Connect_AttachesEmbeddedWindowAndStartsSurfaceActivity()
    {
        FakeSession session = new();
        FakeSurface surface = new();
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);

        Assert.That(host.InitializeSurface(), Is.True);
        Assert.That(await host.InitializeSessionAsync(), Is.True);
        Assert.That(await host.ConnectAsync(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(session.InitializeCalls, Is.EqualTo(1));
            Assert.That(session.ConnectCalls, Is.EqualTo(1));
            Assert.That(session.AttachedParent, Is.EqualTo(surface.Handle));
            Assert.That(session.ResizeCalls, Is.EqualTo(1));
            Assert.That(surface.StartCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task InitializeSession_AttachesManagedSurfaceBeforeProtocolInitialization()
    {
        FakeManagedSession session = new() { IsAvailableBeforeConnect = true };
        FakeSurface surface = new();
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);

        Assert.That(host.InitializeSurface(), Is.True);
        Assert.That(await host.InitializeSessionAsync(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(session.AttachCalls, Is.EqualTo(1));
            Assert.That(session.CallOrder, Is.EqualTo(new[] { "Attach", "Initialize" }));
            Assert.That(session.AttachedParent, Is.EqualTo(surface.Handle));
        });
    }

    [Test]
    public async Task ResizeSurface_ForwardsCurrentBoundsToEmbeddedWindow()
    {
        FakeSession session = new();
        FakeSurface surface = new() { Bounds = new Rectangle(1, 2, 300, 200) };
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);
        host.InitializeSurface();
        await host.InitializeSessionAsync();
        await host.ConnectAsync();

        surface.Bounds = new Rectangle(4, 5, 640, 480);
        surface.RaiseResize();

        Assert.That(session.LastBounds, Is.EqualTo(new EmbeddedWindowBounds(4, 5, 640, 480)));
    }

    [Test]
    public async Task Focus_RetriesEmbeddingWhenChildWindowWasNotReadyAtConnect()
    {
        FakeSession session = new() { AllowAttach = false };
        FakeSurface surface = new();
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);
        host.InitializeSurface();
        await host.InitializeSessionAsync();
        await host.ConnectAsync();

        session.AllowAttach = true;
        host.Focus();

        Assert.Multiple(() =>
        {
            Assert.That(session.AttachCalls, Is.EqualTo(2));
            Assert.That(session.FocusCalls, Is.EqualTo(1));
            Assert.That(session.AttachedParent, Is.EqualTo(surface.Handle));
        });
    }

    [Test]
    public async Task Focus_ReattachesWhenEmbeddedProcessRecreatesItsWindow()
    {
        FakeSession session = new() { WindowHandle = new IntPtr(99) };
        FakeSurface surface = new();
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);
        host.InitializeSurface();
        await host.InitializeSessionAsync();
        await host.ConnectAsync();

        session.WindowHandle = new IntPtr(100);
        host.Focus();

        Assert.That(session.AttachCalls, Is.EqualTo(2));
    }

    [Test]
    public async Task Focus_ReappliesCurrentBoundsWhenTheEmbeddedWindowIsAlreadyAttached()
    {
        FakeSession session = new();
        FakeSurface surface = new() { Bounds = new Rectangle(0, 0, 1280, 720) };
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);
        host.InitializeSurface();
        await host.InitializeSessionAsync();
        await host.ConnectAsync();

        surface.Bounds = new Rectangle(0, 0, 1920, 1000);
        host.Focus();

        Assert.Multiple(() =>
        {
            Assert.That(session.AttachCalls, Is.EqualTo(1));
            Assert.That(session.ResizeCalls, Is.EqualTo(2));
            Assert.That(session.LastBounds, Is.EqualTo(new EmbeddedWindowBounds(0, 0, 1920, 1000)));
        });
    }

    [Test]
    public async Task SessionLifecycleMarshalsWinFormsSurfaceOperationsToAttachContext()
    {
        FakeManagedSession session = new() { IsAvailableBeforeConnect = true };
        FakeSurface surface = new();
        RecordingSynchronizationContext context = new();
        using DesktopSessionHost host = new(CreateDefinition(), session);

        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            host.AttachSurface(surface);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Assert.That(await Task.Run(() => host.InitializeSessionAsync().AsTask()), Is.True);
        Assert.That(context.PostCalls, Is.GreaterThanOrEqualTo(1));
        Assert.That(session.AttachedParent, Is.EqualTo(surface.Handle));
    }

    [Test]
    public async Task SessionLifecycleMarshalsInitializeAndConnectToAttachContext()
    {
        FakeSession session = new();
        FakeSurface surface = new();
        RecordingSynchronizationContext context = new();
        using DesktopSessionHost host = new(CreateDefinition(), session);

        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            host.AttachSurface(surface);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Assert.That(await Task.Run(() => host.InitializeSessionAsync().AsTask()), Is.True);
        Assert.That(await Task.Run(() => host.ConnectAsync().AsTask()), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(context.PostCalls, Is.EqualTo(2));
            Assert.That(session.InitializeCalls, Is.EqualTo(1));
            Assert.That(session.ConnectCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Close_DisposesSessionAndSurfaceExactlyOnce()
    {
        FakeSession session = new();
        FakeSurface surface = new();
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);
        host.InitializeSurface();
        await host.InitializeSessionAsync();

        await host.CloseAsync();
        await host.CloseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(session.CloseCalls, Is.EqualTo(1));
            Assert.That(session.DisposeCalls, Is.EqualTo(1));
            Assert.That(surface.DisposeCalls, Is.EqualTo(1));
            Assert.That(surface.ClearTagCalls, Is.EqualTo(1));
        });
    }

    private static ConnectionDefinition CreateDefinition() => new(
        Guid.NewGuid(), "test", "localhost", 22, ProtocolKind.Ssh2, CredentialReference.None);

    private class FakeSession : IProtocolSession, IEmbeddedWindow
    {
        public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;
        public ProtocolCapabilities Capabilities => ProtocolCapabilities.EmbeddedWindow | ProtocolCapabilities.Resize;
        public bool IsAvailable => IsAvailableBeforeConnect || State == ProtocolSessionState.Connected;
        public IntPtr WindowHandle { get; set; } = new(99);
        public IntPtr AttachedParent { get; private set; }
        public EmbeddedWindowBounds LastBounds { get; private set; }
        public int InitializeCalls { get; private set; }
        public int ConnectCalls { get; private set; }
        public int CloseCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public int ResizeCalls { get; private set; }
        public int AttachCalls { get; private set; }
        public int FocusCalls { get; private set; }
        public bool AllowAttach { get; set; } = true;
        public bool IsAvailableBeforeConnect { get; init; }
        public List<string> CallOrder { get; } = [];

        public bool Initialize()
        {
            CallOrder.Add("Initialize");
            InitializeCalls++;
            State = ProtocolSessionState.Initialized;
            return true;
        }

        public bool Connect()
        {
            ConnectCalls++;
            State = ProtocolSessionState.Connected;
            return true;
        }

        public void Disconnect() => State = ProtocolSessionState.Closing;
        public void Focus() => FocusCalls++;
        public void Focus(IntPtr ownerWindowHandle) => FocusCalls++;

        public void Close()
        {
            CloseCalls++;
            State = ProtocolSessionState.Closed;
        }

        public bool AttachTo(IntPtr parentWindowHandle, TimeSpan timeout)
        {
            CallOrder.Add("Attach");
            AttachCalls++;
            AttachedParent = parentWindowHandle;
            return AllowAttach;
        }

        public void Resize(EmbeddedWindowBounds bounds)
        {
            ResizeCalls++;
            LastBounds = bounds;
        }

        public void Dispose() => DisposeCalls++;
        public ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Initialize());
        public ValueTask<bool> ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Connect());
        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) { Disconnect(); return ValueTask.CompletedTask; }
        public ValueTask CloseAsync(CancellationToken cancellationToken = default) { Close(); return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class FakeManagedSession : FakeSession, IManagedEmbeddedWindow
    {
    }

    private sealed class FakeSurface : IDesktopSessionSurface
    {
        public IntPtr Handle => new(123);
        public bool IsDisposed { get; private set; }
        public bool IsVisible { get; private set; }
        public Rectangle Bounds { get; set; } = new(0, 0, 100, 100);
        public Rectangle ContentBounds => Bounds;
        public int StartCalls { get; private set; }
        public int ClearTagCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public event EventHandler? Resize;

        public void SetParentTag(object? value) { }
        public void ClearParentTag() => ClearTagCalls++;
        public void ShowSurface() => IsVisible = true;
        public void StartActivity() => StartCalls++;
        public void StopActivity() { }

        public void DisposeSurface()
        {
            DisposeCalls++;
            IsDisposed = true;
        }

        public void RaiseResize() => Resize?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        public int SendCalls { get; private set; }
        public int PostCalls { get; private set; }

        public override void Send(SendOrPostCallback d, object? state)
        {
            SendCalls++;
            d(state);
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            PostCalls++;
            d(state);
        }
    }
}
