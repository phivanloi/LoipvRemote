using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Infrastructure.Windows.Interop;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Putty;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.Protocols.Putty;

public sealed class PuttyProtocolFactoryTests
{
    [Test]
    public async Task CreatesSshSessionAndBuildsLaunchArgumentsFromDomain()
    {
        var process = new FakeProcessHost();
        IEmbeddedWindowOperations operations = Substitute.For<IEmbeddedWindowOperations>();
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "ssh", "server.example", 22, ProtocolKind.Ssh2, CredentialReference.None,
            Options: new ConnectionNodeOptions(
                new Dictionary<string, string>
                {
                    ["ExecutablePath"] = "C:\\Tools\\putty.exe",
                    ["Username"] = "operator",
                    ["SSHOptions"] = "-noagent"
                },
                Array.Empty<string>()));

        using IProtocolSession session = new PuttyProtocolFactory(() => process, () => operations).Create(definition);

        Assert.That(await session.InitializeAsync(), Is.True);
        Assert.That(await session.ConnectAsync(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(process.StartOptions?.ExecutablePath, Is.EqualTo("C:\\Tools\\putty.exe"));
            Assert.That(process.StartOptions?.Arguments, Does.Contain("-ssh"));
            Assert.That(process.StartOptions?.Arguments, Does.Contain("server.example"));
            Assert.That(process.StartOptions?.StartMinimized, Is.False);
        });
    }

    [Test]
    public void RejectsMissingExecutablePath()
    {
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "ssh", "server.example", 22, ProtocolKind.Ssh2, CredentialReference.None,
            Options: new ConnectionNodeOptions(
                new Dictionary<string, string>(),
                Array.Empty<string>()));

        Assert.That(
            () => new PuttyProtocolFactory(
                () => new FakeProcessHost(),
                () => Substitute.For<IEmbeddedWindowOperations>(),
                () => null).Create(definition),
            Throws.ArgumentException);
    }

    [Test]
    public async Task UsesNamedPipeForResolvedPasswordInsteadOfCommandLineSecret()
    {
        var process = new FakeProcessHost();
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "ssh", "server.example", 22, ProtocolKind.Ssh2, CredentialReference.None,
            Options: new ConnectionNodeOptions(
                new Dictionary<string, string> { ["ExecutablePath"] = "putty.exe", ["Username"] = "operator" },
                Array.Empty<string>()));

        using IProtocolSession session = new PuttyProtocolFactory(
            () => process,
            () => Substitute.For<IEmbeddedWindowOperations>(),
            passwordResolver: _ => "secret-value",
            passwordPipeFactory: (_, password) =>
            {
                Assert.That(password, Is.EqualTo("secret-value"));
                return "pipe-name";
            }).Create(definition);

        await session.InitializeAsync();
        await session.ConnectAsync();

        Assert.Multiple(() =>
        {
            Assert.That(process.StartOptions?.Arguments, Does.Contain("-pwfile"));
            Assert.That(process.StartOptions?.Arguments, Does.Contain("pipe-name"));
            Assert.That(process.StartOptions?.Arguments, Does.Not.Contain("secret-value"));
        });
    }

    [Test]
    public async Task UsesHostWindowHandleAtLaunchSoPuTTYStartsAsAnEmbeddedChild()
    {
        var process = new FakeProcessHost();
        IEmbeddedWindowOperations operations = Substitute.For<IEmbeddedWindowOperations>();
        var options = new PuttyConnectionOptions(
            "putty.exe",
            new PuttyLaunchOptions
            {
                Hostname = "server.example",
                Port = 22,
                Protocol = PuttyProtocolKind.Ssh,
                SshVersion = PuttySshVersion.Ssh2
            });
        using var session = new PuttyProtocolSession(process, operations, options);

        ((IEmbeddedWindowHost)session).SetHostWindowHandle(new IntPtr(0x1234));
        await session.InitializeAsync();
        await session.ConnectAsync();

        Assert.Multiple(() =>
        {
            Assert.That(process.StartOptions?.Arguments, Does.Contain("-hwndparent"));
            Assert.That(process.StartOptions?.Arguments, Does.Contain("4660"));
        });
    }

    [Test]
    public async Task ExplicitlyReparentsWhenLaunchHintAlreadyMatchesSurface()
    {
        var process = new FakeProcessHost { MainWindowHandle = new IntPtr(42) };
        IEmbeddedWindowOperations operations = Substitute.For<IEmbeddedWindowOperations>();
        var options = new PuttyConnectionOptions(
            "putty.exe",
            new PuttyLaunchOptions
            {
                Hostname = "server.example",
                Port = 22,
                Protocol = PuttyProtocolKind.Ssh,
                SshVersion = PuttySshVersion.Ssh2
            });
        using var session = new PuttyProtocolSession(process, operations, options);

        ((IEmbeddedWindowHost)session).SetHostWindowHandle(new IntPtr(7));
        await session.InitializeAsync();
        await session.ConnectAsync();

        Assert.That(session.AttachTo(new IntPtr(7), TimeSpan.FromSeconds(1)), Is.True);
        operations.Received(1).SetParent(new IntPtr(42), new IntPtr(7));
    }

    [Test]
    public async Task ForwardsKeyboardAndImeInputOnlyWhenSessionIsRunning()
    {
        var process = new FakeProcessHost { MainWindowHandle = new IntPtr(42) };
        IEmbeddedWindowOperations operations = Substitute.For<IEmbeddedWindowOperations>();
        var options = new PuttyConnectionOptions(
            "putty.exe",
            new PuttyLaunchOptions
            {
                Hostname = "server.example",
                Port = 22,
                Protocol = PuttyProtocolKind.Ssh,
                SshVersion = PuttySshVersion.Ssh2
            });
        using var session = new PuttyProtocolSession(process, operations, options);

        Assert.That(session.TryForwardInputMessage(NativeMethods.WM_KEYDOWN, IntPtr.Zero, IntPtr.Zero), Is.False);
        await session.InitializeAsync();
        await session.ConnectAsync();
        operations.TryFocus(new IntPtr(7), new IntPtr(42)).Returns(true);
        session.Focus();
        session.Focus(new IntPtr(7));
        Assert.That(session.TryForwardInputMessage(NativeMethods.WM_KEYDOWN, IntPtr.Zero, IntPtr.Zero), Is.True);
        Assert.That(session.TryForwardInputMessage(NativeMethods.WM_IME_COMPOSITION, IntPtr.Zero, IntPtr.Zero), Is.True);
        operations.Received(2).Activate(new IntPtr(42));
        operations.Received(1).SetFocus(new IntPtr(42));
        operations.Received(1).TryFocus(new IntPtr(7), new IntPtr(42));
        operations.Received(1).SendMessage(new IntPtr(42), (uint)NativeMethods.WM_KEYDOWN, IntPtr.Zero, IntPtr.Zero);
        operations.Received(1).SendMessage(new IntPtr(42), (uint)NativeMethods.WM_IME_COMPOSITION, IntPtr.Zero, IntPtr.Zero);
    }

    [Test]
    public async Task AttachToConvertsTopLevelWindowToBorderlessChildWithoutRestoringIt()
    {
        var process = new FakeProcessHost { MainWindowHandle = new IntPtr(42) };
        IEmbeddedWindowOperations operations = Substitute.For<IEmbeddedWindowOperations>();
        operations.GetWindowStyle(new IntPtr(42)).Returns(
            LoipvRemote.Infrastructure.Windows.Interop.NativeMethods.WS_CAPTION |
            LoipvRemote.Infrastructure.Windows.Interop.NativeMethods.WS_THICKFRAME |
            LoipvRemote.Infrastructure.Windows.Interop.NativeMethods.WS_SYSMENU |
            LoipvRemote.Infrastructure.Windows.Interop.NativeMethods.WS_POPUP);
        var options = new PuttyConnectionOptions(
            "putty.exe",
            new PuttyLaunchOptions { Hostname = "server.example", Port = 22, Protocol = PuttyProtocolKind.Ssh });
        using var session = new PuttyProtocolSession(process, operations, options);

        await session.InitializeAsync();
        await session.ConnectAsync();

        Assert.That(session.AttachTo(new IntPtr(7), TimeSpan.FromSeconds(1)), Is.True);
        Received.InOrder(() =>
        {
            operations.TrySetWindowStyle(new IntPtr(42), Arg.Any<int>());
            operations.SetParent(new IntPtr(42), new IntPtr(7));
            operations.TrySetWindowStyle(new IntPtr(42), Arg.Any<int>());
            operations.RefreshFrame(new IntPtr(42));
        });
        operations.Received(2).TrySetWindowStyle(
            new IntPtr(42),
            PuttyEmbeddedWindowLayout.CreateBorderlessChildStyle(
                LoipvRemote.Infrastructure.Windows.Interop.NativeMethods.WS_CAPTION |
                LoipvRemote.Infrastructure.Windows.Interop.NativeMethods.WS_THICKFRAME |
                LoipvRemote.Infrastructure.Windows.Interop.NativeMethods.WS_SYSMENU |
                LoipvRemote.Infrastructure.Windows.Interop.NativeMethods.WS_POPUP));
        operations.Received(1).SetParent(new IntPtr(42), new IntPtr(7));
        operations.Received(1).RefreshFrame(new IntPtr(42));
        operations.DidNotReceive().Restore(Arg.Any<IntPtr>());
    }

    [Test]
    public async Task AttachToKeepsUsingTheCapturedHandleAfterReparenting()
    {
        var process = new FakeProcessHost { MainWindowHandle = new IntPtr(42) };
        IEmbeddedWindowOperations operations = Substitute.For<IEmbeddedWindowOperations>();
        int originalStyle = NativeMethods.WS_CAPTION |
                            NativeMethods.WS_THICKFRAME |
                            NativeMethods.WS_SYSMENU;
        int borderlessStyle = PuttyEmbeddedWindowLayout.CreateBorderlessChildStyle(originalStyle);
        operations.GetWindowStyle(new IntPtr(42)).Returns(
            originalStyle,
            originalStyle,
            borderlessStyle,
            borderlessStyle);
        operations.When(x => x.SetParent(new IntPtr(42), new IntPtr(7)))
            .Do(_ => process.MainWindowHandle = IntPtr.Zero);
        var options = new PuttyConnectionOptions(
            "putty.exe",
            new PuttyLaunchOptions { Hostname = "server.example", Port = 22, Protocol = PuttyProtocolKind.Ssh });
        using var session = new PuttyProtocolSession(process, operations, options);

        await session.InitializeAsync();
        await session.ConnectAsync();

        Assert.That(session.AttachTo(new IntPtr(7), TimeSpan.FromSeconds(1)), Is.True);
        session.Resize(new EmbeddedWindowBounds(0, 0, 1280, 720));
        session.Focus();
        Assert.That(session.TryForwardInputMessage(0x010F, IntPtr.Zero, IntPtr.Zero), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(session.WindowHandle, Is.EqualTo(new IntPtr(42)));
            operations.Received(2).TrySetWindowStyle(new IntPtr(42), Arg.Any<int>());
            operations.Received(1).RefreshFrame(new IntPtr(42));
            operations.DidNotReceive().Restore(Arg.Any<IntPtr>());
            operations.Received(1).Move(new IntPtr(42), 0, 0, 1280, 720);
            operations.Received(1).Activate(new IntPtr(42));
            operations.Received(1).SetFocus(new IntPtr(42));
            operations.Received(1).SendMessage(new IntPtr(42), 0x010F, IntPtr.Zero, IntPtr.Zero);
        });
    }

    [Test]
    public async Task ReattachesWhenPuTTYRecreatesItsHostedWindow()
    {
        var process = new FakeProcessHost { MainWindowHandle = new IntPtr(42) };
        IEmbeddedWindowOperations operations = Substitute.For<IEmbeddedWindowOperations>();
        int rootWindowLookupCount = 0;
        operations.FindChildWindow(Arg.Any<IntPtr>(), Arg.Any<IntPtr>())
            .Returns(callInfo =>
            {
                if (callInfo.ArgAt<IntPtr>(1) != IntPtr.Zero)
                    return IntPtr.Zero;

                rootWindowLookupCount++;
                return rootWindowLookupCount == 1 ? new IntPtr(42) : new IntPtr(99);
            });
        operations.HasClassName(new IntPtr(42), "PuTTY").Returns(true);
        operations.HasClassName(new IntPtr(99), "PuTTY").Returns(true);
        operations.GetWindowStyle(Arg.Any<IntPtr>()).Returns(NativeMethods.WS_CHILD);
        var options = new PuttyConnectionOptions(
            "putty.exe",
            new PuttyLaunchOptions { Hostname = "server.example", Port = 22, Protocol = PuttyProtocolKind.Ssh });
        using var session = new PuttyProtocolSession(process, operations, options);

        ((IEmbeddedWindowHost)session).SetHostWindowHandle(new IntPtr(7));
        await session.InitializeAsync();
        await session.ConnectAsync();

        Assert.That(session.AttachTo(new IntPtr(7), TimeSpan.FromSeconds(1)), Is.True);
        session.Focus(new IntPtr(7));

        Assert.Multiple(() =>
        {
            Assert.That(session.WindowHandle, Is.EqualTo(new IntPtr(99)));
            operations.Received(1).SetParent(new IntPtr(42), new IntPtr(7));
            operations.Received(1).SetParent(new IntPtr(99), new IntPtr(7));
            operations.Received(1).TryFocus(new IntPtr(7), new IntPtr(99));
        });
    }

    [Test]
    public async Task ResizeKeepsPuTTYExactlyInsideTheEmbeddedClientArea()
    {
        var process = new FakeProcessHost { MainWindowHandle = new IntPtr(42) };
        IEmbeddedWindowOperations operations = Substitute.For<IEmbeddedWindowOperations>();
        operations.GetWindowStyle(new IntPtr(42)).Returns(NativeMethods.WS_CHILD);
        var options = new PuttyConnectionOptions(
            "putty.exe",
            new PuttyLaunchOptions { Hostname = "server.example", Port = 22, Protocol = PuttyProtocolKind.Ssh });
        using var session = new PuttyProtocolSession(process, operations, options);

        await session.InitializeAsync();
        await session.ConnectAsync();
        session.Resize(new EmbeddedWindowBounds(0, 0, 1280, 720));

        operations.Received(1).Move(new IntPtr(42), 0, 0, 1280, 720);
    }

    private sealed class FakeProcessHost : IPuttyProcessHost
    {
        private EventHandler? _exited;
        public bool IsRunning { get; private set; }
        public nint MainWindowHandle { get; set; }
        public string MainWindowTitle => "PuTTY";
        public PuttyProcessStartOptions? StartOptions { get; private set; }

        public bool Start(PuttyProcessStartOptions options, EventHandler exited)
        {
            StartOptions = options;
            _exited = exited;
            IsRunning = true;
            return true;
        }

        public void Stop()
        {
            IsRunning = false;
            _exited?.Invoke(this, EventArgs.Empty);
        }

        public void StopProcess() => Stop();

        public void Refresh() { }
        public void Dispose() => Stop();
    }
}
