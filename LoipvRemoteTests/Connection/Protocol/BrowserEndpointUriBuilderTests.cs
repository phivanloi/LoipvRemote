using LoipvRemote.Protocols.Browser;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

public class BrowserEndpointUriBuilderTests
{
    [Test]
    public void BuildsDefaultHttpEndpoint()
    {
        var endpoint = BrowserEndpointUriBuilder.Build("host.example", 80, "http", 80);

        Assert.That(endpoint.AbsoluteUri, Is.EqualTo("http://host.example/"));
    }

    [Test]
    public void AppliesCustomPortBeforeExistingPath()
    {
        var endpoint = BrowserEndpointUriBuilder.Build("https://host.example/admin", 8443, "https", 443);

        Assert.That(endpoint.AbsoluteUri, Is.EqualTo("https://host.example:8443/admin"));
    }

    [Test]
    public void SessionOwnsEndpointConstructionAndNavigationLifecycle()
    {
        var client = new FakeBrowserClient();
        var session = new BrowserSession(client);

        Assert.That(session.Initialize(new BrowserConnectionOptions("host.example", 8080, "http", 80)), Is.True);
        Assert.That(session.Connect(), Is.True);

        Assert.That(client.Endpoint?.AbsoluteUri, Is.EqualTo("http://host.example:8080/"));
        Assert.That(session.State, Is.EqualTo(LoipvRemote.Domain.Protocols.ProtocolSessionState.Connected));
    }

    [Test]
    public void SessionDoesNotNavigateBeforeInitialization()
    {
        var client = new FakeBrowserClient();

        Assert.That(new BrowserSession(client).Connect(), Is.False);
        Assert.That(client.Endpoint, Is.Null);
    }

    private sealed class FakeBrowserClient : IBrowserClient
    {
        public Uri? Endpoint { get; private set; }

        public void Navigate(Uri endpoint) => Endpoint = endpoint;
    }
}
