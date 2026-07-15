using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Putty;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.Protocols.Putty;

public sealed class PuttyProtocolFactoryTests
{
    [Test]
    public void CreatesSshSessionAndBuildsLaunchArgumentsFromDomain()
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

        Assert.That(session.Initialize(), Is.True);
        Assert.That(session.Connect(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(process.StartOptions?.ExecutablePath, Is.EqualTo("C:\\Tools\\putty.exe"));
            Assert.That(process.StartOptions?.Arguments, Does.Contain("-ssh"));
            Assert.That(process.StartOptions?.Arguments, Does.Contain("server.example"));
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
    public void UsesNamedPipeForResolvedPasswordInsteadOfCommandLineSecret()
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

        session.Initialize();
        session.Connect();

        Assert.Multiple(() =>
        {
            Assert.That(process.StartOptions?.Arguments, Does.Contain("-pwfile"));
            Assert.That(process.StartOptions?.Arguments, Does.Contain("pipe-name"));
            Assert.That(process.StartOptions?.Arguments, Does.Not.Contain("secret-value"));
        });
    }

    [Test]
    public void ForwardsImeInputOnlyWhenSessionIsRunning()
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

        Assert.That(session.TryForwardInputMessage(0x010F, IntPtr.Zero, IntPtr.Zero), Is.False);
        session.Initialize();
        session.Connect();
        operations.TryFocus(new IntPtr(7), new IntPtr(42)).Returns(true);
        session.Focus();
        session.Focus(new IntPtr(7));
        Assert.That(session.TryForwardInputMessage(0x010F, IntPtr.Zero, IntPtr.Zero), Is.True);
        operations.Received(2).Activate(new IntPtr(42));
        operations.Received(1).SetFocus(new IntPtr(42));
        operations.Received(1).TryFocus(new IntPtr(7), new IntPtr(42));
        operations.Received(1).SendMessage(new IntPtr(42), 0x010F, IntPtr.Zero, IntPtr.Zero);
    }

    private sealed class FakeProcessHost : IPuttyProcessHost
    {
        private EventHandler? _exited;
        public bool IsRunning { get; private set; }
        public nint MainWindowHandle { get; init; }
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
