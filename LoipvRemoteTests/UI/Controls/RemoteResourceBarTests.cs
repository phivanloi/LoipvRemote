using LoipvRemote.Connection;
using LoipvRemote.UI.Controls;
using LoipvRemote.Connection.Monitoring;
using LoipvRemote.Protocols.Putty.Monitoring;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Controls
{
    [TestFixture]
    public sealed class RemoteResourceBarTests
    {
        [Test]
        public void KeepsStatusVisibleWhenTheMonitoringStripIsCreated()
        {
            using RemoteResourceBar bar = new(
                new ConnectionInfo(),
                new PuttyResourceMonitorFactory(Substitute.For<IPuttyHostKeyTrustStore>()));

            Assert.That(bar.IsStatusVisible, Is.True);
        }
    }
}
