using LoipvRemote.Config.Connections;
using NUnit.Framework;

namespace LoipvRemoteTests.Config.Connections
{
    [TestFixture]
    public sealed class AsyncSaveRequestQueueTests
    {
        [Test]
        public void CoalescesRequestsAndKeepsTheLatestPropertyName()
        {
            AsyncSaveRequestQueue queue = new();

            Assert.That(queue.Queue("Name"), Is.True);
            Assert.That(queue.Queue("Port"), Is.False);
            Assert.That(queue.TryTake(out string propertyName), Is.True);
            Assert.That(propertyName, Is.EqualTo("Port"));
            Assert.That(queue.CompleteSaveAndHasPendingRequest(), Is.False);
        }

        [Test]
        public void KeepsTheWorkerAliveWhenARequestArrivesDuringSave()
        {
            AsyncSaveRequestQueue queue = new();
            _ = queue.Queue("Protocol");
            _ = queue.TryTake(out _);

            Assert.That(queue.Queue("Port"), Is.False);
            Assert.That(queue.CompleteSaveAndHasPendingRequest(), Is.True);
            Assert.That(queue.TryTake(out string propertyName), Is.True);
            Assert.That(propertyName, Is.EqualTo("Port"));
            Assert.That(queue.CompleteSaveAndHasPendingRequest(), Is.False);
        }
    }
}
