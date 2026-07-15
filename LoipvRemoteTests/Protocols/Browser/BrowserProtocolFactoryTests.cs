using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Browser;
using NUnit.Framework;

namespace LoipvRemoteTests.Protocols.Browser;

public sealed class BrowserProtocolFactoryTests
{
    [TestCase(ProtocolKind.Http, "http", 80)]
    [TestCase(ProtocolKind.Https, "https", 443)]
    public void CreatesHttpAndHttpsSessions(ProtocolKind protocol, string scheme, int defaultPort)
    {
        var client = new RecordingBrowserClient();
        var factory = new BrowserProtocolFactory(() => client);
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "browser", "server.example", defaultPort, protocol, CredentialReference.None);

        IProtocolSession session = factory.Create(definition);

        Assert.That(session, Is.TypeOf<BrowserProtocolSession>());
        BrowserProtocolSession browser = (BrowserProtocolSession)session;
        Assert.Multiple(() =>
        {
            Assert.That(browser.Options.Scheme, Is.EqualTo(scheme));
            Assert.That(browser.Options.DefaultPort, Is.EqualTo(defaultPort));
        });
    }

    [Test]
    public void BrowserKindUsesExplicitSchemeOption()
    {
        var options = new ConnectionNodeOptions(
            new Dictionary<string, string> { ["Scheme"] = "http" },
            Array.Empty<string>());
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "browser", "server.example", 8080, ProtocolKind.Browser, CredentialReference.None,
            Options: options);

        var session = (BrowserProtocolSession)new BrowserProtocolFactory(() => new RecordingBrowserClient()).Create(definition);

        Assert.Multiple(() =>
        {
            Assert.That(session.Options.Scheme, Is.EqualTo("http"));
            Assert.That(session.Options.DefaultPort, Is.EqualTo(80));
        });
    }

    [Test]
    public void BrowserKindRejectsUnsupportedScheme()
    {
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "browser", "server.example", 443, ProtocolKind.Browser, CredentialReference.None,
            Options: new ConnectionNodeOptions(
                new Dictionary<string, string> { ["Scheme"] = "file" },
                Array.Empty<string>()));

        Assert.That(
            () => new BrowserProtocolFactory(() => new RecordingBrowserClient()).Create(definition),
            Throws.ArgumentException);
    }

    [Test]
    public void SessionForwardsLifecycleToBrowserClient()
    {
        var client = new RecordingBrowserClient();
        using var session = new BrowserProtocolSession(
            client,
            new BrowserConnectionOptions("server.example", 443, "https", 443));

        Assert.Multiple(() =>
        {
            Assert.That(session.Initialize(), Is.True);
            Assert.That(session.Connect(), Is.True);
            Assert.That(client.LastUri?.ToString(), Is.EqualTo("https://server.example/"));
            Assert.That(session.State, Is.EqualTo(LoipvRemote.Domain.Protocols.ProtocolSessionState.Connected));
        });

        session.Close();
        Assert.That(session.State, Is.EqualTo(LoipvRemote.Domain.Protocols.ProtocolSessionState.Closed));
    }

    private sealed class RecordingBrowserClient : IBrowserClient
    {
        public Uri? LastUri { get; private set; }

        public void Navigate(Uri endpoint) => LastUri = endpoint;
    }
}
