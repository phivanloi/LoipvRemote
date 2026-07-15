using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.ExternalApps;
using NUnit.Framework;

namespace LoipvRemoteTests.Protocols.ExternalApps;

[TestFixture]
public sealed class ExternalApplicationProtocolFactoryTests
{
    [Test]
    public void CreatesOnlyExternalApplicationSessions()
    {
        FakeHostFactory hostFactory = new();
        ExternalApplicationProtocolFactory factory = new(hostFactory);
        ConnectionDefinition definition = new(
            Guid.NewGuid(),
            "tool",
            "localhost",
            0,
            ProtocolKind.ExternalApplication,
            CredentialReference.None,
            new ExternalApplicationDefinition(
                "tool",
                "tool.exe",
                string.Empty,
                string.Empty,
                false,
                false,
                false));

        using IProtocolSession session = factory.Create(definition);

        Assert.That(session, Is.TypeOf<ExternalApplicationSession>());
        Assert.That(hostFactory.CreateCount, Is.EqualTo(1));
    }

    [Test]
    public void RejectsOtherProtocolKindsAtModuleBoundary()
    {
        ExternalApplicationProtocolFactory factory = new(new FakeHostFactory());
        ConnectionDefinition definition = new(
            Guid.NewGuid(), "ssh", "localhost", 22, ProtocolKind.Ssh2, CredentialReference.None);

        Assert.That(() => factory.Create(definition), Throws.TypeOf<NotSupportedException>());
    }

    private sealed class FakeHostFactory : IExternalApplicationHostFactory
    {
        public int CreateCount { get; private set; }

        public IExternalApplicationHost Create()
        {
            CreateCount++;
            return new FakeHost();
        }
    }

    private sealed class FakeHost : IExternalApplicationHost
    {
        public bool IsRunning => false;
        public IntPtr WindowHandle => IntPtr.Zero;
        public string WindowTitle => string.Empty;
        public event EventHandler? Exited;

        public bool Start(ExternalApplicationDefinition definition) => false;
        public bool WaitForMainWindow(TimeSpan timeout) => false;
        public bool AttachTo(IntPtr parentWindowHandle) => false;
        public void Resize(EmbeddedWindowBounds bounds) { }
        public void Focus() { }
        public void Close() { }
        public void Dispose() => Exited = null;
    }
}
