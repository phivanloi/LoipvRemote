using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Core;

public sealed class ConnectionOptionInheritanceResolverTests
{
    [Test]
    public void ResolveUsesFolderValuesForExplicitlyInheritedConnectionProperties()
    {
        Guid folderId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        ConnectionTreeDefinition tree = new(
            [new ConnectionFolderDefinition(folderId, "Production", Options: new ConnectionNodeOptions(
                new Dictionary<string, string> { ["Username"] = "operator", ["SmartSize"] = "true" }, []))],
            [new ConnectionDefinition(connectionId, "SSH", "ssh.example", 22, ProtocolKind.Ssh2, CredentialReference.None,
                ParentFolderId: folderId,
                Options: new ConnectionNodeOptions(new Dictionary<string, string> { ["Username"] = "ignored", ["Theme"] = "dark" }, ["Username"]))]);

        ConnectionDefinition resolved = ConnectionOptionInheritanceResolver.Resolve(tree, connectionId);

        Assert.That(resolved.Options!.Values, Is.EqualTo(new Dictionary<string, string>
        {
            ["Username"] = "operator",
            ["SmartSize"] = "true",
            ["Theme"] = "dark"
        }));
        Assert.That(resolved.Options.InheritedProperties, Is.Empty);
    }
}
