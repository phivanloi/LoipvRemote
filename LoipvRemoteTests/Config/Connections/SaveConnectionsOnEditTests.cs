using LoipvRemote.Config.Connections;
using LoipvRemote.Container;
using NUnit.Framework;

namespace LoipvRemoteTests.Config.Connections
{
    [TestFixture]
    public sealed class SaveConnectionsOnEditTests
    {
        [Test]
        public void FolderExpansionStateIsAlwaysPersisted()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SaveConnectionsOnEdit.ShouldPersistImmediately(nameof(ContainerInfo.IsExpanded)), Is.True);
                Assert.That(SaveConnectionsOnEdit.ShouldPersistImmediately("Name"), Is.False);
                Assert.That(SaveConnectionsOnEdit.ShouldPersistImmediately(null), Is.False);
            });
        }
    }
}
