using LoipvRemote.Connection;
using LoipvRemote.UI.Controls;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Controls
{
    [TestFixture]
    public sealed class RemoteResourceBarTests
    {
        [Test]
        public void KeepsStatusVisibleWhenTheMonitoringStripIsCreated()
        {
            using RemoteResourceBar bar = new(new ConnectionInfo());

            Assert.That(bar.IsStatusVisible, Is.True);
        }
    }
}
