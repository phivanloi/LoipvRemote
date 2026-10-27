using LoipvRemote.Domain.Connections;
using LoipvRemote.WinUI.Sessions;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Sessions;

public sealed class ConnectionDialogValidationTests
{
    [TestCase(0)]
    [TestCase(65536)]
    [TestCase(22.5)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void BothConnectionFormsRejectInvalidPorts(double port)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConnectionDialogValidation.GetError("Test", "localhost", ProtocolKind.Ssh2, port), Is.Not.Null);
            Assert.That(ConnectionDialogValidation.GetQuickConnectError("localhost", ProtocolKind.Ssh2, port), Is.Not.Null);
        });
    }

    [TestCase(1)]
    [TestCase(22)]
    [TestCase(3389)]
    [TestCase(5900)]
    [TestCase(65535)]
    public void BothConnectionFormsAcceptValidPorts(double port)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConnectionDialogValidation.GetError("Test", "localhost", ProtocolKind.Ssh2, port), Is.Null);
            Assert.That(ConnectionDialogValidation.GetQuickConnectError("localhost", ProtocolKind.Ssh2, port), Is.Null);
        });
    }

    [Test]
    public void BothConnectionFormsRejectUnknownProtocols()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConnectionDialogValidation.GetError("Test", "localhost", (ProtocolKind)999, 22), Is.Not.Null);
            Assert.That(ConnectionDialogValidation.GetQuickConnectError("localhost", (ProtocolKind)999, 22), Is.Not.Null);
        });
    }
}
