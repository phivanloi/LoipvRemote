using System;
using LoipvRemote.Connection.Protocol;
using LoipvRemote.Connection.Protocol.SSH;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.ExternalApps;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.Messages;
using LoipvRemote.UI.Adapters;
using LoipvRemote.Tools;
using LoipvRemote.Infrastructure.Windows.ProcessManagement;
using LoipvRemoteTests.TestHelpers;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

public class ProtocolFactoryDomainAdapterTests
{
    [Test]
    public void CreatesSshSessionFromDomainDefinition()
    {
        IProtocolFactory factory = new ProtocolFactory(new ExternalCredentialConnectorRegistry([]), TestSecretStore.Instance, new MessageCollector(), new ConnectionWorkspaceAdapter(), new ExternalToolsService(), new WindowsExternalApplicationHostFactory());
        var definition = new ConnectionDefinition(
            Guid.NewGuid(),
            "ssh",
            "host.example",
            22,
            ProtocolKind.Ssh2,
            CredentialReference.None);

        var session = factory.Create(definition);

        Assert.That(session, Is.InstanceOf<ProtocolSSH2>());
    }

    [Test]
    public void CreatesExternalApplicationSessionFromDomainDefinition()
    {
        IProtocolFactory factory = new ProtocolFactory(new ExternalCredentialConnectorRegistry([]), TestSecretStore.Instance, new MessageCollector(), new ConnectionWorkspaceAdapter(), new ExternalToolsService(), new WindowsExternalApplicationHostFactory());
        var definition = new ConnectionDefinition(
            Guid.NewGuid(),
            "tool",
            "host.example",
            0,
            ProtocolKind.ExternalApplication,
            CredentialReference.None,
            new ExternalApplicationDefinition(
                "tool",
                "tool.exe",
                "--host host.example",
                string.Empty,
                RunElevated: false,
                EmbedWindow: true,
                WaitForExit: false));

        var session = factory.Create(definition);

        Assert.That(session, Is.InstanceOf<ExternalApplicationSession>());
    }

    [Test]
    public void RejectsExternalAppWithoutCommandDefinition()
    {
        IProtocolFactory factory = new ProtocolFactory(new ExternalCredentialConnectorRegistry([]), TestSecretStore.Instance, new MessageCollector(), new ConnectionWorkspaceAdapter(), new ExternalToolsService(), new WindowsExternalApplicationHostFactory());
        var definition = new ConnectionDefinition(
            Guid.NewGuid(),
            "tool",
            "host.example",
            0,
            ProtocolKind.ExternalApplication,
            CredentialReference.None);

        Assert.That(() => factory.Create(definition), Throws.InstanceOf<ArgumentException>());
    }
}
