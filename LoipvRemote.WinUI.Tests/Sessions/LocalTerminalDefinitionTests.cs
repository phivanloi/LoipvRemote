using LoipvRemote.Domain.Connections;
using LoipvRemote.WinUI.Sessions;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Sessions;

public sealed class LocalTerminalDefinitionTests
{
    [Test]
    public void SidebarItemIsStablePresentationOnlyTerminal()
    {
        var first = LocalTerminalDefinition.CreateSidebarItem();
        var second = LocalTerminalDefinition.CreateSidebarItem();

        Assert.Multiple(() =>
        {
            Assert.That(first.Id, Is.EqualTo(second.Id));
            Assert.That(first.DisplayName, Is.EqualTo("Terminal"));
            Assert.That(first.IsFolder, Is.False);
            Assert.That(first.IsLocalTerminal, Is.True);
            Assert.That(first.Protocol, Is.EqualTo(ProtocolKind.Ssh2));
        });
    }

    [Test]
    public void SidebarItemShowsConnectedStateWhileAnyTerminalTabIsOpen()
    {
        var item = LocalTerminalDefinition.CreateSidebarItem(hasOpenTerminalTab: true);

        Assert.Multiple(() =>
        {
            Assert.That(item.IsLocalTerminal, Is.True);
            Assert.That(item.IsConnected, Is.True);
        });
    }

    [Test]
    public void EveryActivationCreatesAnIndependentRuntimeDefinition()
    {
        var first = LocalTerminalDefinition.CreateSession(1);
        var second = LocalTerminalDefinition.CreateSession(2);

        Assert.Multiple(() =>
        {
            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            Assert.That(first.Name, Is.EqualTo("Terminal"));
            Assert.That(second.Name, Is.EqualTo("Terminal 2"));
            Assert.That(LocalTerminalDefinition.IsLocalTerminal(first), Is.True);
            Assert.That(LocalTerminalDefinition.IsLocalTerminal(second), Is.True);
        });
    }

    [Test]
    public void OrdinarySshDefinitionIsNotLocalTerminal()
    {
        var definition = new ConnectionDefinition(
            Guid.NewGuid(),
            "server",
            "server.example",
            22,
            ProtocolKind.Ssh2,
            LoipvRemote.Domain.Credentials.CredentialReference.None);

        Assert.That(LocalTerminalDefinition.IsLocalTerminal(definition), Is.False);
    }
}
