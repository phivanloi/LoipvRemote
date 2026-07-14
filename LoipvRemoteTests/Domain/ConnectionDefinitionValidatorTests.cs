using System;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Validation;
using NUnit.Framework;

namespace LoipvRemoteTests.Domain;

public class ConnectionDefinitionValidatorTests
{
    [Test]
    public void AcceptsValidDefinitionWithoutSecret()
    {
        var definition = new ConnectionDefinition(Guid.NewGuid(), "prod-ssh", "server.example", 22,
            ProtocolKind.Ssh2, CredentialReference.None);

        Assert.DoesNotThrow(() => ConnectionDefinitionValidator.Validate(definition));
    }

    [TestCase("", "server.example", 22)]
    [TestCase("prod", "", 22)]
    [TestCase("prod", "server.example", 65536)]
    public void RejectsInvalidDefinitions(string name, string host, int port)
    {
        var definition = new ConnectionDefinition(Guid.NewGuid(), name, host, port,
            ProtocolKind.Ssh2, CredentialReference.None);

        Assert.That(() => ConnectionDefinitionValidator.Validate(definition), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void RejectsExternalApplicationWithoutApplicationDefinition()
    {
        var definition = new ConnectionDefinition(Guid.NewGuid(), "tool", "host", 0,
            ProtocolKind.ExternalApplication, CredentialReference.None);

        Assert.That(() => ConnectionDefinitionValidator.Validate(definition), Throws.ArgumentException);
    }

    [Test]
    public void AcceptsExternalApplicationWithValidDefinition()
    {
        var application = new ExternalApplicationDefinition(
            "Terminal", "terminal.exe", "--host example", string.Empty,
            RunElevated: false, EmbedWindow: true, WaitForExit: false);
        var definition = new ConnectionDefinition(Guid.NewGuid(), "tool", "host", 0,
            ProtocolKind.ExternalApplication, CredentialReference.None, application);

        Assert.DoesNotThrow(() => ConnectionDefinitionValidator.Validate(definition));
    }

    [Test]
    public void AcceptsExternalApplicationWithoutHostname()
    {
        var application = new ExternalApplicationDefinition(
            "Terminal", "terminal.exe", string.Empty, string.Empty,
            RunElevated: false, EmbedWindow: false, WaitForExit: true);
        var definition = new ConnectionDefinition(Guid.NewGuid(), "terminal", string.Empty, 0,
            ProtocolKind.ExternalApplication, CredentialReference.None, application);

        Assert.DoesNotThrow(() => ConnectionDefinitionValidator.Validate(definition));
    }
}
