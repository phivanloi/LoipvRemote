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
    public void Connect_AttachesEmbeddedWindowAndStartsSurfaceActivity()
    {
        FakeSession session = new();
        FakeSurface surface = new();
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);

        Assert.That(host.InitializeSurface(), Is.True);
        Assert.That(host.InitializeSession(), Is.True);
        Assert.That(host.Connect(), Is.True);

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
    public void InitializeSession_AttachesManagedSurfaceBeforeProtocolInitialization()
    {
        FakeManagedSession session = new() { IsAvailableBeforeConnect = true };
        FakeSurface surface = new();
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);

        Assert.That(host.InitializeSurface(), Is.True);
        Assert.That(host.InitializeSession(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(session.AttachCalls, Is.EqualTo(1));
            Assert.That(session.CallOrder, Is.EqualTo(new[] { "Attach", "Initialize" }));
            Assert.That(session.AttachedParent, Is.EqualTo(surface.Handle));
        });
    }

    [Test]
    public void ResizeSurface_ForwardsCurrentBoundsToEmbeddedWindow()
    {
        FakeSession session = new();
        FakeSurface surface = new() { Bounds = new Rectangle(1, 2, 300, 200) };
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);
        host.InitializeSurface();
        host.InitializeSession();
        host.Connect();

        surface.Bounds = new Rectangle(4, 5, 640, 480);
        surface.RaiseResize();

        Assert.That(session.LastBounds, Is.EqualTo(new EmbeddedWindowBounds(4, 5, 640, 480)));
    }

    [Test]
    public void Focus_RetriesEmbeddingWhenChildWindowWasNotReadyAtConnect()
    {
        FakeSession session = new() { AllowAttach = false };
        FakeSurface surface = new();
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);
        host.InitializeSurface();
        host.InitializeSession();
        host.Connect();

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
    public void Focus_ReattachesWhenEmbeddedProcessRecreatesItsWindow()
    {
        FakeSession session = new() { WindowHandle = new IntPtr(99) };
        FakeSurface surface = new();
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);
        host.InitializeSurface();
        host.InitializeSession();
        host.Connect();

        session.WindowHandle = new IntPtr(100);
        host.Focus();

        Assert.That(session.AttachCalls, Is.EqualTo(2));
    }

    [Test]
    public void Close_DisposesSessionAndSurfaceExactlyOnce()
    {
        FakeSession session = new();
        FakeSurface surface = new();
        using DesktopSessionHost host = new(CreateDefinition(), session);
        host.AttachSurface(surface);
        host.InitializeSurface();
        host.InitializeSession();

        host.Close();
        host.Close();

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
}
