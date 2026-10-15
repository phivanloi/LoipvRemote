using LoipvRemote.Application.Sessions;
using LoipvRemote.Domain.Connections;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Core;

public sealed class QuickConnectionDefinitionFactoryTests
{
    [Test]
    public void CreateBuildsAnEphemeralDefinitionWithoutACredentialReference()
    {
        ConnectionNodeOptions options = new(new Dictionary<string, string> { ["Username"] = "operator" }, []);

        ConnectionDefinition definition = QuickConnectionDefinitionFactory.Create(" ssh.example ", 22, ProtocolKind.Ssh2, options);

        Assert.Multiple(() =>
        {
            Assert.That(definition.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(definition.Name, Is.EqualTo("Quick: ssh.example"));
            Assert.That(definition.Host, Is.EqualTo("ssh.example"));
            Assert.That(definition.Credential, Is.EqualTo(Domain.Credentials.CredentialReference.None));
            Assert.That(definition.Options, Is.SameAs(options));
        });
    }

    [Test]
    public void CreateRejectsInvalidEndpointValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => QuickConnectionDefinitionFactory.Create(" ", 22, ProtocolKind.Ssh2), Throws.ArgumentException);
            Assert.That(() => QuickConnectionDefinitionFactory.Create("ssh.example", 70000, ProtocolKind.Ssh2), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }
}
