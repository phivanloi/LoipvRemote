using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.UI.Window;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Window
{
    [TestFixture]
    public class ConnectionSelectionHelperTests
    {
        [Test]
        public void GetDirectChildConnections_ExcludesNestedConnections()
        {
            var folder = new ContainerInfo { Name = "Root" };
            var directConnection = new ConnectionInfo { Name = "Direct" };
            var nestedFolder = new ContainerInfo { Name = "Nested" };
            var nestedConnection = new ConnectionInfo { Name = "Nested connection" };
            nestedFolder.AddChild(nestedConnection);
            folder.AddChild(directConnection);
            folder.AddChild(nestedFolder);

            var result = ConnectionSelectionHelper.GetDirectChildConnections(folder);

            Assert.That(result, Is.EqualTo(new[] { directConnection }));
        }

        [Test]
        public void GetDirectChildConnections_WithNonContainer_ReturnsEmpty()
        {
            var result = ConnectionSelectionHelper.GetDirectChildConnections(new ConnectionInfo());

            Assert.That(result, Is.Empty);
        }
    }
}
