using LoipvRemote.Config.Connections.Multiuser;
using NUnit.Framework;

namespace LoipvRemoteTests.Config.Connections.Multiuser;

public sealed class ConnectionsUpdateAvailableEventArgsTests
{
    [Test]
    public void RejectsMissingRevision()
    {
        Assert.That(() => new ConnectionsUpdateAvailableEventArgs(" "), Throws.ArgumentException);
    }

    [Test]
    public void PreservesRevision()
    {
        var eventArgs = new ConnectionsUpdateAvailableEventArgs("A1B2C3");

        Assert.That(eventArgs.Revision, Is.EqualTo("A1B2C3"));
    }
}
