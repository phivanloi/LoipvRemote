using LoipvRemote.Domain.Connections;
using NUnit.Framework;

namespace LoipvRemoteTests.Domain;

[TestFixture]
public sealed class ProtocolDefaultsTests
{
    [TestCase(ProtocolKind.Rdp, 3389)]
    [TestCase(ProtocolKind.Vnc, 5900)]
    [TestCase(ProtocolKind.Ard, 5900)]
    [TestCase(ProtocolKind.Ssh1, 22)]
    [TestCase(ProtocolKind.Ssh2, 22)]
    [TestCase(ProtocolKind.Telnet, 23)]
    [TestCase(ProtocolKind.Raw, 23)]
    [TestCase(ProtocolKind.Rlogin, 513)]
    [TestCase(ProtocolKind.Http, 80)]
    [TestCase(ProtocolKind.Https, 443)]
    [TestCase(ProtocolKind.PowerShell, 5985)]
    [TestCase(ProtocolKind.Terminal, 0)]
    [TestCase(ProtocolKind.Wsl, 0)]
    [TestCase(ProtocolKind.AnyDesk, 0)]
    [TestCase(ProtocolKind.ExternalApplication, 0)]
    public void ReturnsStablePortForProtocol(ProtocolKind protocol, int expectedPort)
    {
        Assert.That(ProtocolDefaults.GetDefaultPort(protocol), Is.EqualTo(expectedPort));
    }
}
