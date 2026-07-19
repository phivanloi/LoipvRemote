using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Protocols.Rdp;
using LoipvRemote.Infrastructure.Windows.Com;
using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Putty;
using LoipvRemote.Protocols.Rdp;
using LoipvRemote.Protocols.Vnc;
using NSubstitute;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace LoipvRemote.WinUI.Tests.Core;

public sealed class ProtocolFactoryTests
{
    [TestCase(12, 96, 12)]
    [TestCase(12, 144, 12)]
    [TestCase(12, 192, 12)]
    [TestCase(12, 288, 12)]
    [TestCase(7, 120, 10)]
    [TestCase(30, 120, 18)]
    public void PuttyFontScalingKeepsTerminalTextBalancedWithTheUi(int savedHeight, int dpi, int expectedHeight)
    {
        Assert.That(PuttyFontScaling.GetFontHeight(savedHeight, (uint)dpi), Is.EqualTo(expectedHeight));
    }

    [Test]
    public async Task PuttyFactoryBuildsSshArgumentsWithoutAddingPasswordToTheCommandLine()
    {
        var process = new RecordingPuttyProcessHost();
        ConnectionDefinition definition = new(
            Guid.NewGuid(), "ssh", "server.example", 22, ProtocolKind.Ssh2, CredentialReference.None,
            Options: new ConnectionNodeOptions(
                new Dictionary<string, string> { ["ExecutablePath"] = "putty.exe", ["Username"] = "operator" },
                Array.Empty<string>()));

        using IProtocolSession session = new PuttyProtocolFactory(
            () => process,
            () => Substitute.For<IEmbeddedWindowOperations>(),
            passwordResolver: _ => "secret-value",
            passwordPipeFactory: (_, _) => "pipe-name",
            endpointProbeFactory: () => new NoopPuttyEndpointProbe()).Create(definition);

        Assert.That(await session.InitializeAsync(), Is.True);
        Assert.That(await session.ConnectAsync(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(process.StartOptions?.Arguments, Does.Contain("-ssh"));
            Assert.That(process.StartOptions?.Arguments, Does.Contain("-pwfile \\\\.\\PIPE\\pipe-name"));
            Assert.That(process.StartOptions?.Arguments, Does.Not.Contain("secret-value"));
        });
    }

    [Test]
    public async Task PuttySessionEmbedsAsBorderlessChildAndForwardsKeyboardInput()
    {
        var process = new RecordingPuttyProcessHost(windowHandle: (IntPtr)42);
        IEmbeddedWindowOperations windows = Substitute.For<IEmbeddedWindowOperations>();
        const int topLevelStyle = unchecked((int)0x80CF0000);
        windows.GetWindowStyle((IntPtr)42).Returns(topLevelStyle);

        using var session = new PuttyProtocolSession(
            process,
            windows,
            new PuttyConnectionOptions(
                "putty.exe",
                new PuttyLaunchOptions
                {
                    Hostname = "server.example",
                    Port = 22,
                    Username = "operator"
                }),
            new NoopPuttyEndpointProbe());

        Assert.That(await session.InitializeAsync(), Is.True);
        session.SetHostWindowHandle((IntPtr)99);
        Assert.That(await session.ConnectAsync(), Is.True);
        Assert.That(process.StartOptions?.Arguments, Does.Not.Contain("-hwndparent"));
        Assert.That(process.StartOptions?.StartHidden, Is.False);
        Assert.That(session.AttachTo((IntPtr)99, TimeSpan.Zero), Is.True);
        session.Resize(new EmbeddedWindowBounds(0, 0, 1280, 720));
        session.Focus((IntPtr)7);

        Assert.Multiple(() =>
        {
            Assert.That(session.TryForwardInputMessage(0x0102, (IntPtr)'x', IntPtr.Zero), Is.True);
            Assert.That(session.TryForwardInputMessage(0x000F, IntPtr.Zero, IntPtr.Zero), Is.False);
        });
        windows.Received().SetParent((IntPtr)42, (IntPtr)99);
        windows.Received().TrySetWindowStyle(
            (IntPtr)42,
            PuttyEmbeddedWindowLayout.CreateBorderlessChildStyle(topLevelStyle));
        windows.Received().RefreshFrame((IntPtr)42);
        windows.Received().Move((IntPtr)42, -7, -40, 1295, 768);
        windows.Received().TryFocus((IntPtr)7, (IntPtr)42);
        windows.Received().SetFocus((IntPtr)42);
        windows.Received().SendMessage((IntPtr)42, 0x0102, (IntPtr)'x', IntPtr.Zero);
    }

    [Test]
    public async Task PuttySessionSelectsItsOwnWindowWhenSeveralSessionsShareTheNativeHost()
    {
        var process = new RecordingPuttyProcessHost(windowHandle: (IntPtr)10, processId: 200);
        IEmbeddedWindowOperations windows = Substitute.For<IEmbeddedWindowOperations>();
        windows.FindChildWindow((IntPtr)99, IntPtr.Zero).Returns((IntPtr)41);
        windows.FindChildWindow((IntPtr)99, (IntPtr)41).Returns((IntPtr)42);
        windows.HasClassName(Arg.Any<IntPtr>(), "PuTTY").Returns(true);
        windows.GetWindowProcessId((IntPtr)41).Returns(100u);
        windows.GetWindowProcessId((IntPtr)42).Returns(200u);

        using var session = new PuttyProtocolSession(
            process,
            windows,
            new PuttyConnectionOptions("putty.exe", new PuttyLaunchOptions { Hostname = "server.example", Port = 22 }),
            new NoopPuttyEndpointProbe());

        Assert.That(await session.InitializeAsync(), Is.True);
        session.SetHostWindowHandle((IntPtr)99);
        Assert.That(await session.ConnectAsync(), Is.True);
        Assert.That(session.AttachTo((IntPtr)99, TimeSpan.Zero), Is.True);

        windows.Received().SetParent((IntPtr)42, (IntPtr)99);
    }

    [Test]
    public async Task PuttySessionDoesNotReshowOrReactivateAnAlreadyAttachedWindow()
    {
        var process = new RecordingPuttyProcessHost(windowHandle: (IntPtr)42);
        IEmbeddedWindowOperations windows = Substitute.For<IEmbeddedWindowOperations>();
        const int topLevelStyle = unchecked((int)0x80CF0000);
        windows.GetWindowStyle((IntPtr)42).Returns(topLevelStyle);

        using var session = new PuttyProtocolSession(
            process,
            windows,
            new PuttyConnectionOptions("putty.exe", new PuttyLaunchOptions { Hostname = "server.example", Port = 22 }),
            new NoopPuttyEndpointProbe());

        Assert.That(await session.InitializeAsync(), Is.True);
        session.SetHostWindowHandle((IntPtr)99);
        Assert.That(await session.ConnectAsync(), Is.True);
        Assert.That(session.AttachTo((IntPtr)99, TimeSpan.Zero), Is.True);

        session.Focus((IntPtr)7);
        session.Focus((IntPtr)7);
        session.Resize(new EmbeddedWindowBounds(0, 0, 1280, 720));
        session.Resize(new EmbeddedWindowBounds(0, 0, 1280, 720));

        windows.Received(1).Show((IntPtr)42);
        windows.Received(1).Activate((IntPtr)42);
    }

    [Test]
    public async Task RdpFactoryUsesRdpClientAndRejectsDifferentProtocol()
    {
        var client = new RecordingRdpClient();
        ConnectionDefinition definition = new(
            Guid.NewGuid(), "rdp", "server.example", 3389, ProtocolKind.Rdp, CredentialReference.None,
            Options: new ConnectionNodeOptions(new Dictionary<string, string> { ["RdpVersion"] = RdpVersion.Rdc11.ToString() }, Array.Empty<string>()));

        using IProtocolSession session = new RdpProtocolFactory(_ => client).Create(definition);

        Assert.That(await session.InitializeAsync(), Is.True);
        Assert.That(await session.ConnectAsync(), Is.True);
        Assert.That(client.ConnectCalls, Is.EqualTo(1));

        ConnectionDefinition invalid = definition with { Protocol = ProtocolKind.Ssh2 };
        Assert.That(() => new RdpProtocolFactory(_ => client).Create(invalid), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public async Task RdpFactoryAppliesClipboardAndLocalDriveRedirectionForFileTransfer()
    {
        var client = new RecordingRdpClient();
        ConnectionDefinition definition = new(
            Guid.NewGuid(), "rdp-transfer", "server.example", 3389, ProtocolKind.Rdp, CredentialReference.None,
            Options: new ConnectionNodeOptions(
                new Dictionary<string, string>
                {
                    ["RedirectClipboard"] = "true",
                    ["RedirectDiskDrives"] = RDPDiskDrives.Local.ToString(),
                    ["CacheBitmaps"] = "true",
                    ["Colors"] = RDPColors.Colors32Bit.ToString()
                },
                Array.Empty<string>()));

        using IProtocolSession session = new RdpProtocolFactory(_ => client).Create(definition);

        Assert.That(await session.InitializeAsync(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(client.RuntimeConfiguration, Is.Not.Null);
            Assert.That(client.RuntimeConfiguration!.RedirectClipboard, Is.True);
            Assert.That(client.RuntimeConfiguration.DriveRedirection, Is.EqualTo(RdpDriveRedirection.Local));
            Assert.That(client.RuntimeConfiguration.CacheBitmaps, Is.True);
            Assert.That(client.RuntimeConfiguration.ColorDepth, Is.EqualTo(32));
        });
    }

    [Test]
    public async Task PuttySessionDoesNotStartNativeProcessWhenEndpointProbeFails()
    {
        var process = new RecordingPuttyProcessHost();
        using var session = new PuttyProtocolSession(
            process,
            Substitute.For<IEmbeddedWindowOperations>(),
            new PuttyConnectionOptions(
                "putty.exe",
                new PuttyLaunchOptions { Hostname = "127.0.0.1", Port = 22 }),
            new FailingPuttyEndpointProbe());

        Assert.That(await session.InitializeAsync(), Is.True);
        Assert.ThrowsAsync<SocketException>(async () => await session.ConnectAsync());
        Assert.That(process.StartOptions, Is.Null);
    }

    [Test]
    public async Task RdpSessionWaitsForTheNativeConnectedEventBeforeCompleting()
    {
        var client = new EventDrivenRdpClient();
        using var session = new RdpProtocolSession(
            client,
            new RdpConnectionOptions("server.example", 3389));

        Assert.That(await session.InitializeAsync(), Is.True);

        ValueTask<bool> connecting = session.ConnectAsync();

        Assert.That(connecting.IsCompleted, Is.False);
        client.RaiseConnected();
        Assert.That(await connecting, Is.True);
    }

    [Test]
    public async Task RdpFactoryKeepsLegacyFullscreenDefinitionsInsideTheEmbeddedSurface()
    {
        var client = new RecordingRdpClient();
        ConnectionDefinition definition = new(
            Guid.NewGuid(), "rdp", "server.example", 3389, ProtocolKind.Rdp, CredentialReference.None,
            Options: new ConnectionNodeOptions(
                new Dictionary<string, string> { ["Resolution"] = "Fullscreen" },
                Array.Empty<string>()));

        using IProtocolSession session = new RdpProtocolFactory(_ => client).Create(definition);

        Assert.That(await session.InitializeAsync(), Is.True);
        Assert.That(client.Display, Is.EqualTo(new RdpDisplayConfiguration(1920, 1080, false, true, 100, 100)));
    }

    [TestCase(800, 600, 1.5, 1024, 768, 150)]
    [TestCase(1919, 1079, 1.0, 1920, 1080, 100)]
    [TestCase(1928, 1088, 1.0, 1928, 1088, 100)]
    [TestCase(5000, 3000, 2.1, 3840, 2160, 200)]
    public void RdpAutoDisplayUsesDpiAwareProductBounds(
        int width,
        int height,
        double rasterizationScale,
        int expectedWidth,
        int expectedHeight,
        int expectedDesktopScale)
    {
        RdpDisplayConfiguration display = RdpDisplaySizing.CreateAuto(width, height, rasterizationScale);

        Assert.That(display, Is.EqualTo(new RdpDisplayConfiguration(
            expectedWidth,
            expectedHeight,
            false,
            true,
            (uint)expectedDesktopScale,
            100)));
    }

    [Test]
    public async Task RdpSessionPreparesInitialDisplayAndUpdatesConnectedDisplayWithoutReconnect()
    {
        var client = new DynamicDisplayRdpClient();
        using var session = new RdpProtocolSession(client, new RdpConnectionOptions("server.example", 3389));
        var initial = new RdpDisplayConfiguration(2560, 1440, false, true, 150, 100);
        var resized = new RdpDisplayConfiguration(1920, 1080, false, true, 100, 100);

        Assert.That(await session.InitializeAsync(), Is.True);
        ((IAdaptiveRdpDisplaySession)session).PrepareDisplay(initial);
        Assert.That(client.Display, Is.EqualTo(initial));

        Assert.That(await session.ConnectAsync(), Is.True);
        Assert.That(((IAdaptiveRdpDisplaySession)session).TryUpdateDisplay(resized), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(client.DynamicDisplay, Is.EqualTo(resized));
            Assert.That(client.ConnectCalls, Is.EqualTo(1));
        });
    }

    [Test]
    [Platform("Win")]
    public void NativeRdpHostCanCreateTheWindowsActiveXControlInTheNativeSessionHost()
    {
        using var host = new NativeProbeWindow();
        using var client = new RdpActiveXRuntime(RdpVersion.Rdc10);

        Assert.That(client.AttachTo(host.Handle, TimeSpan.Zero), Is.True);
        Assert.That(() => client.Initialize(), Throws.Nothing);
        Assert.That(client.IsAvailable, Is.True);
    }

    [Test]
    [Platform("Win")]
    public void NativeRdpHostEnablesMasterRedirectionSwitchesForLocalFileTransfer()
    {
        using var host = new NativeProbeWindow();
        using var client = new RdpActiveXRuntime(RdpVersion.Rdc10);

        Assert.That(client.AttachTo(host.Handle, TimeSpan.Zero), Is.True);
        client.Initialize();
        client.ApplyConfiguration(new RdpRuntimeConfiguration
        {
            Server = "server.example",
            Port = 3389,
            RedirectClipboard = true,
            DriveRedirection = RdpDriveRedirection.Local
        });

        Assert.Multiple(() =>
        {
            Assert.That(client.RedirectClipboardEnabled, Is.True);
            Assert.That(client.RedirectDrivesEnabled, Is.True);
        });
    }

    [Test]
    [Platform("Win")]
    public void NativeRdpHostKeepsCustomDriveRedirectionUsableWhenSelectingIndividualDrives()
    {
        using var host = new NativeProbeWindow();
        using var client = new RdpActiveXRuntime(RdpVersion.Rdc10);

        Assert.That(client.AttachTo(host.Handle, TimeSpan.Zero), Is.True);
        client.Initialize();

        Assert.That(() => client.ApplyConfiguration(new RdpRuntimeConfiguration
        {
            Server = "server.example",
            Port = 3389,
            DriveRedirection = RdpDriveRedirection.Custom,
            CustomDrives = "C"
        }), Throws.Nothing);
        Assert.That(client.RedirectDrivesEnabled, Is.True);
    }

    [Test]
    [Platform("Win")]
    public void NativeSessionHostShowsOnlyTheActiveProtocolChild()
    {
        using var parent = new NativeProbeWindow();
        using var host = new WindowsChildWindowHost(parent.Handle);
        IntPtr firstChild = CreateWindowEx(0, "STATIC", string.Empty, WsChild | WsVisible, 0, 0, 1, 1,
            host.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        IntPtr secondChild = CreateWindowEx(0, "STATIC", string.Empty, WsChild | WsVisible, 0, 0, 1, 1,
            host.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        try
        {
            Assert.That(firstChild, Is.Not.EqualTo(IntPtr.Zero));
            Assert.That(secondChild, Is.Not.EqualTo(IntPtr.Zero));

            host.SetVisible(visible: true);
            host.SetChildVisible(firstChild, visible: false);
            host.SetChildVisible(secondChild, visible: true);

            Assert.Multiple(() =>
            {
                Assert.That(IsWindowVisible(firstChild), Is.False);
                Assert.That(IsWindowVisible(secondChild), Is.True);
            });
        }
        finally
        {
            if (firstChild != IntPtr.Zero)
                _ = DestroyWindow(firstChild);
            if (secondChild != IntPtr.Zero)
                _ = DestroyWindow(secondChild);
        }
    }

    [Test]
    public async Task VncFactoryAppliesDomainOptionsAndResolvesSecretAtProtocolBoundary()
    {
        var client = new RecordingVncClient();
        ConnectionDefinition definition = new(
            Guid.NewGuid(), "vnc", "server.example", 5901, ProtocolKind.Vnc, CredentialReference.None,
            Options: new ConnectionNodeOptions(
                new Dictionary<string, string> { ["ViewOnly"] = "true", ["SmartSize"] = "false" },
                Array.Empty<string>()));

        using IProtocolSession session = new VncProtocolFactory(
            () => client,
            () => new NoopVncEndpointProbe(),
            passwordResolver: _ => "runtime-secret").Create(definition);

        Assert.That(await session.InitializeAsync(), Is.True);
        Assert.That(await session.ConnectAsync(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(client.Host, Is.EqualTo("server.example"));
            Assert.That(client.Port, Is.EqualTo(5901));
            Assert.That(client.ViewOnly, Is.True);
            Assert.That(client.SmartSize, Is.False);
        });
    }

    [Test]
    public async Task VncSessionForwardsUiOperationsThroughTheClientContract()
    {
        var client = new RecordingVncClient();
        var session = new VncProtocolSession(
            client,
            new NoopVncEndpointProbe(),
            new VncConnectionOptions("server.example", 5900, ViewOnly: false, SmartSize: true, Password: "runtime-secret"));

        Assert.That(await session.InitializeAsync(), Is.True);
        Assert.That(await session.ConnectAsync(), Is.True);
        session.Focus();
        session.RefreshScreen();
        session.SendSpecialKeys(RemoteSpecialKey.CtrlAltDel);

        Assert.Multiple(() =>
        {
            Assert.That(client.PasswordProvider!(), Is.EqualTo("runtime-secret"));
            Assert.That(client.FocusCalls, Is.EqualTo(1));
            Assert.That(client.RefreshCalls, Is.EqualTo(1));
            Assert.That(client.SpecialKeys, Is.EqualTo([VncSpecialKeys.CtrlAltDel]));
        });
    }

    [Test]
    [Platform("Win")]
    public void NativeVncHostCreatesAndResizesAWinUIChildWindow()
    {
        using var parent = new NativeProbeWindow();
        using var client = new NativeVncClient();
        var session = new VncProtocolSession(
            client,
            new NoopVncEndpointProbe(),
            new VncConnectionOptions("server.example", 5900, ViewOnly: false, SmartSize: true));

        Assert.That(session.AttachTo(parent.Handle, TimeSpan.Zero), Is.True);
        session.Resize(new EmbeddedWindowBounds(0, 0, 320, 200));

        Assert.Multiple(() =>
        {
            Assert.That(client.IsAvailable, Is.True);
            Assert.That(client.WindowHandle, Is.Not.EqualTo(IntPtr.Zero));
            Assert.That(GetParent(client.WindowHandle), Is.EqualTo(parent.Handle));
        });
    }

    [Test]
    [Platform("Win")]
    public async Task NativeVncClientCompletesAnRfbHandshake()
    {
        using var parent = new NativeProbeWindow();
        using var server = new NoneSecurityVncServer();
        using var client = new NativeVncClient();
        Task served = server.ServeOnceAsync();

        Assert.That(client.AttachTo(parent.Handle, TimeSpan.Zero), Is.True);
        client.SetPort(server.Port);
        await client.ConnectAsync(IPAddress.Loopback.ToString(), viewOnly: false, smartSize: true);
        await client.DisconnectAsync();
        await served.WaitAsync(TimeSpan.FromSeconds(5));
    }


    private sealed class RecordingPuttyProcessHost(nint windowHandle = default, int processId = 1) : IPuttyProcessHost
    {
        public bool IsRunning { get; private set; }
        public int ProcessId => processId;
        public nint MainWindowHandle => windowHandle;
        public string MainWindowTitle => "PuTTY";
        public PuttyProcessStartOptions? StartOptions { get; private set; }

        public bool Start(PuttyProcessStartOptions options, EventHandler exited)
        {
            StartOptions = options;
            IsRunning = true;
            return true;
        }

        public void StopProcess() => IsRunning = false;
        public void Refresh() { }
        public void Dispose() => StopProcess();
    }

    private sealed class FailingPuttyEndpointProbe : IPuttyEndpointProbe
    {
        public Task ProbeAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default) =>
            throw new SocketException((int)SocketError.ConnectionRefused);
    }

    private sealed class NoopPuttyEndpointProbe : IPuttyEndpointProbe
    {
        public Task ProbeAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingRdpClient : IRdpClient, IRdpRuntimeClient
    {
        public int ConnectCalls { get; private set; }
        public RdpDisplayConfiguration? Display { get; private set; }
        public RdpRuntimeConfiguration? RuntimeConfiguration { get; private set; }
        public void Initialize() { }
        public void ConfigureEndpoint(string host, int port) { }
        public void Connect() => ConnectCalls++;
        public void Disconnect() { }
        public void ApplyConfiguration(RdpRuntimeConfiguration configuration) => RuntimeConfiguration = configuration;
        public void ApplyDisplay(RdpDisplayConfiguration display) => Display = display;
    }

    private sealed class DynamicDisplayRdpClient : IRdpClient, IRdpRuntimeClient, IRdpDynamicDisplayClient
    {
        public int ConnectCalls { get; private set; }
        public RdpDisplayConfiguration? Display { get; private set; }
        public RdpDisplayConfiguration? DynamicDisplay { get; private set; }

        public void Initialize() { }
        public void ConfigureEndpoint(string host, int port) { }
        public void Connect() => ConnectCalls++;
        public void Disconnect() { }
        public void ApplyConfiguration(RdpRuntimeConfiguration configuration) { }
        public void ApplyDisplay(RdpDisplayConfiguration display) => Display = display;
        public bool TryUpdateDisplay(RdpDisplayConfiguration display)
        {
            DynamicDisplay = display;
            return true;
        }
    }

#pragma warning disable CS0067 // Required IRdpEventClient events are unused by this focused fake.
    private sealed class EventDrivenRdpClient : IRdpClient, IRdpEventClient
    {
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
        public string GetErrorDescription(int disconnectReason) => $"Disconnect {disconnectReason}";
        public void SubscribeEvents() { }
        public void UnsubscribeEvents() { }
        public void RaiseConnected() => Connected?.Invoke(this, EventArgs.Empty);
    }
#pragma warning restore CS0067

    private sealed class RecordingVncClient : IVncClient
    {
        public int Port { get; private set; }
        public string? Host { get; private set; }
        public bool ViewOnly { get; private set; }
        public bool SmartSize { get; private set; }
        public Func<string>? PasswordProvider { get; private set; }
        public int FocusCalls { get; private set; }
        public int RefreshCalls { get; private set; }
        public List<VncSpecialKeys> SpecialKeys { get; } = [];
        public void SetPort(int port) => Port = port;
        public void Connect(string host, bool viewOnly, bool smartSize)
        {
            Host = host;
            ViewOnly = viewOnly;
            SmartSize = smartSize;
        }

        public void Disconnect() { }
        public void SetPasswordProvider(Func<string>? passwordProvider) => PasswordProvider = passwordProvider;
        public void Focus() => FocusCalls++;
        public void RefreshScreen() => RefreshCalls++;
        public void SendSpecialKeys(VncSpecialKeys keys) => SpecialKeys.Add(keys);
    }

    private sealed class NoopVncEndpointProbe : IVncEndpointProbe
    {
        public Task ProbeAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NativeProbeWindow : IDisposable
    {
        public NativeProbeWindow()
        {
            Handle = CreateWindowEx(0, "STATIC", string.Empty, WsChild | WsVisible, 0, 0, 1, 1,
                GetDesktopWindow(), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (Handle == IntPtr.Zero)
                throw new InvalidOperationException("Could not create the native RDP test host.");
        }

        public IntPtr Handle { get; }

        public void Dispose() => _ = DestroyWindow(Handle);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(int extendedStyle, string className, string windowName, int style, int x, int y, int width, int height, IntPtr parentWindowHandle, IntPtr menu, IntPtr instance, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr windowHandle);
    }

    private sealed class NoneSecurityVncServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public NoneSecurityVncServer() => _listener.Start();

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public async Task ServeOnceAsync()
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync();
            await using NetworkStream stream = client.GetStream();
            await stream.WriteAsync("RFB 003.008\n"u8.ToArray());
            await ReadExactlyAsync(stream, 12);
            await stream.WriteAsync(new byte[] { 1, 1 });
            await ReadExactlyAsync(stream, 1);
            await stream.WriteAsync(new byte[] { 0, 0, 0, 0 });
            await ReadExactlyAsync(stream, 1);
            await stream.WriteAsync(new byte[] {
                0, 1, 0, 1,
                32, 24, 0, 1, 0, 255, 0, 255, 0, 255, 0, 16, 8, 0, 0, 0,
                0, 0, 0, 0 });
            await stream.FlushAsync();

            byte[] buffer = new byte[64];
            try
            {
                while (await stream.ReadAsync(buffer) != 0)
                {
                }
            }
            catch (IOException)
            {
                // Closing the client may reset the loopback socket on Windows.
            }
        }

        public void Dispose() => _listener.Stop();

        private static async Task ReadExactlyAsync(NetworkStream stream, int count)
        {
            byte[] data = new byte[count];
            await stream.ReadExactlyAsync(data);
        }

    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr windowHandle);

    private const int WsChild = unchecked((int)0x40000000);
    private const int WsVisible = unchecked((int)0x10000000);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int extendedStyle, string className, string windowName, int style, int x, int y, int width, int height, IntPtr parentWindowHandle, IntPtr menu, IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr windowHandle);

}
