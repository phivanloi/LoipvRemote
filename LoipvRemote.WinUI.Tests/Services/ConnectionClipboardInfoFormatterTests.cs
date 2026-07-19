using LoipvRemote.Application.Credentials;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.WinUI.Services;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Services;

public sealed class ConnectionClipboardInfoFormatterTests
{
    [Test]
    public void FormatUsesTheExactRequestedShapeAndConfiguredUsername()
    {
        var connection = new ConnectionDefinition(
            Guid.NewGuid(),
            "Production SSH",
            " 10.11.12.24 ",
            2222,
            ProtocolKind.Ssh2,
            CredentialReference.None,
            Options: new ConnectionNodeOptions(
                new Dictionary<string, string> { ["Username"] = " ubuntu " },
                []));

        string result = ConnectionClipboardInfoFormatter.Format(connection, [], "secret value");

        Assert.That(result, Is.EqualTo("10.11.12.24:2222 | ubuntu / secret value"));
    }

    [Test]
    public void FormatFallsBackToTheReferencedSharedCredentialUsername()
    {
        Guid credentialId = Guid.NewGuid();
        var connection = new ConnectionDefinition(
            Guid.NewGuid(),
            "Production RDP",
            "rdp.example",
            3389,
            ProtocolKind.Rdp,
            CredentialReference.LocalDpapi(credentialId));
        LocalCredentialDefinition[] credentials =
        [
            new(credentialId, "Production account", "administrator")
        ];

        string result = ConnectionClipboardInfoFormatter.Format(connection, credentials, "p@ssword");

        Assert.That(result, Is.EqualTo("rdp.example:3389 | administrator / p@ssword"));
    }

    [Test]
    public void FormatKeepsTheRequestedSeparatorsWhenCredentialsAreMissing()
    {
        var connection = new ConnectionDefinition(
            Guid.NewGuid(),
            "SSH",
            "ssh.example",
            22,
            ProtocolKind.Ssh2,
            CredentialReference.None);

        string result = ConnectionClipboardInfoFormatter.Format(connection, [], password: null);

        Assert.That(result, Is.EqualTo("ssh.example:22 |  / "));
    }

    [Test]
    public void ConnectionContextMenuIncludesCopyInfoOnlyForConnections()
    {
        string sourceRoot = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI"));
        string code = File.ReadAllText(Path.Combine(sourceRoot, "MainWindow.xaml.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(code, Does.Contain("CreateContextMenuItem(\"Copy info\", Symbol.Copy, CopySelectedConnectionInfoButton_Click)"));
            Assert.That(code, Does.Contain("Clipboard.SetContent(content)"));
        });
    }
}
