using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Tree;
using NUnit.Framework;


namespace LoipvRemoteTests.Tree
{
    public class ConnectionTreeModelTests
    {
        private ConnectionTreeModel _connectionTreeModel;

        [SetUp]
        public void Setup()
        {
            _connectionTreeModel = new ConnectionTreeModel();
        }

        [TearDown]
        public void Teardown()
        {
            _connectionTreeModel = null;
        }

        [Test]
        public void GetChildListProvidesAllChildren()
        {
            var root = new ContainerInfo();
            var folder1 = new ContainerInfo();
            var folder2 = new ContainerInfo();
            var con1 = new ConnectionInfo();
            root.AddChild(folder1);
            folder1.AddChild(folder2);
            root.AddChild(con1);
            _connectionTreeModel.AddRootNode(root);
            var connectionList = ConnectionTreeModel.GetRecursiveChildList(root);
            Assert.That(connectionList, Is.EquivalentTo(new[] {folder1,folder2,con1}));
        }
    }
}
