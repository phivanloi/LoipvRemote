using LoipvRemote.Application.Configuration;
using LoipvRemote.Application.Credentials;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.WinUI.Services;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Services;

public sealed class PortableConnectionCredentialImporterTests
{
    [Test]
    public void ApplyProtectsPortableCredentialsForTheImportedConnectionId()
    {
        Guid sourceConnectionId = Guid.NewGuid();
        Guid importedConnectionId = Guid.NewGuid();
        var originalSecret = "$dpapi-secret:Password";
        var tree = new ConnectionTreeDefinition(
            [],
            [new ConnectionDefinition(
                importedConnectionId,
                "Imported",
                "server.example",
                3389,
                ProtocolKind.Rdp,
                CredentialReference.LocalDpapi(Guid.NewGuid()),
                Options: new ConnectionNodeOptions(
                    new Dictionary<string, string>
                    {
                        ["Username"] = "old-user",
                        [originalSecret] = "source-machine-secret"
                    },
                    []),
                GatewayCredential: CredentialReference.LocalDpapi(Guid.NewGuid()))]);

        ConnectionTreeDefinition result = new PortableConnectionCredentialImporter(new RecordingSecretStore()).Apply(
            tree,
            new Dictionary<Guid, Guid> { [sourceConnectionId] = importedConnectionId },
            new Dictionary<Guid, PortableConnectionCredential>
            {
                [sourceConnectionId] = new("administrator", "connection-password", "gateway-password")
            });

        ConnectionDefinition connection = result.Connections.Single();
        Assert.Multiple(() =>
        {
            Assert.That(connection.Credential, Is.EqualTo(CredentialReference.None));
            Assert.That(connection.GatewayCredential, Is.Null);
            Assert.That(connection.Options!.Values["Username"], Is.EqualTo("administrator"));
            Assert.That(connection.Options.Values[originalSecret], Is.Not.EqualTo("source-machine-secret"));
            Assert.That(connection.Options.Values["$dpapi-secret:Password"], Is.EqualTo($"protected:connection-secret:{importedConnectionId:D}:Password:connection-password"));
            Assert.That(connection.Options.Values["$dpapi-secret:RDGatewayPassword"], Is.EqualTo($"protected:connection-secret:{importedConnectionId:D}:RDGatewayPassword:gateway-password"));
        });
    }

    private sealed class RecordingSecretStore : IStringSecretStore
    {
        public string Protect(string plaintext, string purpose) => $"protected:{purpose}:{plaintext}";

        public string Unprotect(string protectedValue, string purpose) => throw new NotSupportedException();
    }
}
