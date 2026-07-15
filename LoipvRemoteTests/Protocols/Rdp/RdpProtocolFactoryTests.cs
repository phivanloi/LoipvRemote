using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Protocols.Rdp;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Rdp;
using NUnit.Framework;

namespace LoipvRemoteTests.Protocols.Rdp;

public sealed class RdpProtocolFactoryTests
{
    [Test]
    public void CreatesRdpSessionFromDomainDefinition()
    {
        var client = new FakeRdpClient();
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "rdp", "server.example", 3389, ProtocolKind.Rdp, CredentialReference.None);

        using IProtocolSession session = new RdpProtocolFactory(_ => client).Create(definition);

        Assert.That(session, Is.TypeOf<RdpProtocolSession>());
        Assert.That(session.Initialize(), Is.True);
        Assert.That(session.Connect(), Is.True);
        Assert.That(client.ConnectCalls, Is.EqualTo(1));
    }

    [Test]
    public void RejectsNonRdpProtocol()
    {
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "ssh", "server.example", 22, ProtocolKind.Ssh2, CredentialReference.None);

        Assert.That(
            () => new RdpProtocolFactory(_ => new FakeRdpClient()).Create(definition),
            Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void CreatesTheActiveXVersionRequestedByTheConnectionDefinition()
    {
        RdpVersion? requestedVersion = null;
        ConnectionNodeOptions options = new(
            new Dictionary<string, string> { ["RdpVersion"] = RdpVersion.Rdc11.ToString() },
            Array.Empty<string>());
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "rdp", "server.example", 3389, ProtocolKind.Rdp,
            CredentialReference.None, Options: options);

        using IProtocolSession session = new RdpProtocolFactory(version =>
        {
            requestedVersion = version;
            return new FakeRdpClient();
        }).Create(definition);

        Assert.That(requestedVersion, Is.EqualTo(RdpVersion.Rdc11));
    }

    [Test]
    public void AppliesThePersistedRdpSecurityDisplayAndRedirectionOptions()
    {
        ConnectionNodeOptions options = new(
            new Dictionary<string, string>
            {
                ["UseCredSsp"] = "true",
                ["RDPAuthenticationLevel"] = AuthenticationLevel.AuthRequired.ToString(),
                ["Colors"] = RDPColors.Colors32Bit.ToString(),
                ["RedirectClipboard"] = "true",
                ["RedirectPrinters"] = "true",
                ["RedirectDiskDrives"] = RDPDiskDrives.Custom.ToString(),
                ["RedirectDiskDrivesCustom"] = "C;D",
                ["CacheBitmaps"] = "true",
                ["DisplayWallpaper"] = "false",
                ["DisplayThemes"] = "false",
                ["EnableFontSmoothing"] = "true"
            },
            Array.Empty<string>());
        var definition = new ConnectionDefinition(
            Guid.NewGuid(), "rdp", "server.example", 3390, ProtocolKind.Rdp,
            CredentialReference.None, Options: options);
        var client = new RuntimeRdpClient();
        using IProtocolSession session = new RdpProtocolFactory(_ => client).Create(definition);

        Assert.That(session.Initialize(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(client.Configuration?.Server, Is.EqualTo("server.example"));
            Assert.That(client.Configuration?.Port, Is.EqualTo(3390));
            Assert.That(client.Configuration?.EnableCredSsp, Is.True);
            Assert.That(client.Configuration?.AuthenticationLevel, Is.EqualTo((uint)AuthenticationLevel.AuthRequired));
            Assert.That(client.Configuration?.ColorDepth, Is.EqualTo((int)RDPColors.Colors32Bit));
            Assert.That(client.Configuration?.RedirectClipboard, Is.True);
            Assert.That(client.Configuration?.RedirectPrinters, Is.True);
            Assert.That(client.Configuration?.DriveRedirection, Is.EqualTo(RdpDriveRedirection.Custom));
            Assert.That(client.Configuration?.CustomDrives, Is.EqualTo("C;D"));
            Assert.That(client.Configuration?.PerformanceFlags,
                Is.EqualTo((int)(RDPPerformanceFlags.DisableWallpaper |
                                 RDPPerformanceFlags.DisableThemes |
                                 RDPPerformanceFlags.EnableFontSmoothing)));
        });
    }

    private sealed class FakeRdpClient : IRdpClient
    {
        public int ConnectCalls { get; private set; }

        public void Initialize() { }

        public void ConfigureEndpoint(string host, int port) { }

        public void Connect() => ConnectCalls++;

        public void Disconnect() { }
    }

    private sealed class RuntimeRdpClient : IRdpClient, IRdpRuntimeClient
    {
        public RdpRuntimeConfiguration? Configuration { get; private set; }
        public void Initialize() { }
        public void ConfigureEndpoint(string host, int port) { }
        public void Connect() { }
        public void Disconnect() { }
        public void ApplyConfiguration(RdpRuntimeConfiguration configuration) => Configuration = configuration;
        public void ApplyDisplay(RdpDisplayConfiguration display) { }
    }
}
