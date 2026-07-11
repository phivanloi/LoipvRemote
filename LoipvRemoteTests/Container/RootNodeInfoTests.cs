using LoipvRemote.Connection;
using LoipvRemote.Tree.Root;
using NUnit.Framework;

namespace LoipvRemoteTests.Container
{
	public class RootNodeInfoTests
    {
        [Test]
        public void InheritanceIsDisabledForNodesDirectlyUnderRootNode()
        {
	        var expected = "UnInheritedValue";
	        var rootNode = new RootNodeInfo(RootNodeType.Connection)
	        {
		        Description = "thisCameFromTheRootNode"
	        };
	        var con1 = new ConnectionInfo
            {
	            Description = expected,
	            Inheritance = { Description = true }
            };
            rootNode.AddChild(con1);
            Assert.That(con1.Description, Is.EqualTo(expected));
        }
    }
}
