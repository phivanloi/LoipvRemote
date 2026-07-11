using System.Linq;
using System.Threading;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Tools.Clipboard;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;
using LoipvRemote.UI.Controls;
using LoipvRemote.UI.Controls.ConnectionTree;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Controls
{
	public class ConnectionTreeTests
	{
		private LoipvRemote.UI.Controls.ConnectionTree.ConnectionTree _connectionTree;

		[SetUp]
		public void Setup()
		{
			_connectionTree = new LoipvRemote.UI.Controls.ConnectionTree.ConnectionTree();
		}

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void CannotAddConnectionToPuttySessionNode()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
            var puttyRoot = new RootNodeInfo(RootNodeType.PuttySessions);
	        connectionTreeModel.AddRootNode(root);
	        connectionTreeModel.AddRootNode(puttyRoot);

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
	        _connectionTree.ExpandAll();

			_connectionTree.SelectedObject = puttyRoot;
	        _connectionTree.AddConnection();

	        Assert.That(puttyRoot.Children, Is.Empty);
	    }

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void CannotAddFolderToPuttySessionNode()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
	        var puttyRoot = new RootNodeInfo(RootNodeType.PuttySessions);
	        connectionTreeModel.AddRootNode(root);
	        connectionTreeModel.AddRootNode(puttyRoot);

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
	        _connectionTree.ExpandAll();

			_connectionTree.SelectedObject = puttyRoot;
	        _connectionTree.AddFolder();

	        Assert.That(puttyRoot.Children, Is.Empty);
	    }

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void CannotDuplicateRootConnectionNode()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
	        connectionTreeModel.AddRootNode(root);
	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
	        _connectionTree.ExpandAll();

			_connectionTree.SelectedObject = root;
            _connectionTree.DuplicateSelectedNode();

	        Assert.That(connectionTreeModel.RootNodes, Has.One.Items);
	    }

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void CanDuplicateConnectionNode()
	    {
		    var connectionTreeModel = new ConnectionTreeModel();
		    var root = new RootNodeInfo(RootNodeType.Connection);
			var con1 = new ConnectionInfo();
			root.AddChild(con1);
		    connectionTreeModel.AddRootNode(root);
		    _connectionTree.ConnectionTreeModel = connectionTreeModel;
		    _connectionTree.ExpandAll();

			_connectionTree.SelectedObject = con1;
		    _connectionTree.DuplicateSelectedNode();

		    Assert.That(root.Children, Has.Exactly(2).Items);
	    }

		[Test]
	    [Apartment(ApartmentState.STA)]
	    public void CannotDuplicateRootPuttyNode()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var puttyRoot = new RootNodeInfo(RootNodeType.PuttySessions);
	        connectionTreeModel.AddRootNode(puttyRoot);
	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
	        _connectionTree.ExpandAll();

			_connectionTree.SelectedObject = puttyRoot;
	        _connectionTree.DuplicateSelectedNode();

	        Assert.That(connectionTreeModel.RootNodes, Has.One.Items);
	    }

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void CannotDuplicatePuttyConnectionNode()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var puttyRoot = new RootNodeInfo(RootNodeType.PuttySessions);
            var puttyConnection = new PuttySessionInfo();
            puttyRoot.AddChild(puttyConnection);
	        connectionTreeModel.AddRootNode(puttyRoot);
	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
	        _connectionTree.ExpandAll();

            _connectionTree.SelectedObject = puttyConnection;
	        _connectionTree.DuplicateSelectedNode();

	        Assert.That(puttyRoot.Children, Has.One.Items);
	    }

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void DuplicatingWithNoNodeSelectedDoesNothing()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var puttyRoot = new RootNodeInfo(RootNodeType.PuttySessions);
	        connectionTreeModel.AddRootNode(puttyRoot);
	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
	        _connectionTree.ExpandAll();

			_connectionTree.SelectedObject = null;
	        _connectionTree.DuplicateSelectedNode();

	        Assert.That(connectionTreeModel.RootNodes, Has.One.Items);
	    }

	    [Test]
	    [Apartment(ApartmentState.STA)]
	    public void ExpandingAllItemsUpdatesColumnWidthAppropriately()
	    {
            var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
            connectionTreeModel.AddRootNode(root);
	        ContainerInfo parent = root;
	        foreach (var i in Enumerable.Repeat("", 8))
	        {
                var newContainer = new ContainerInfo {IsExpanded = false};
                parent.AddChild(newContainer);
	            parent = newContainer;
	        }

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;

	        var widthBefore = _connectionTree.Columns[0].Width;
	        _connectionTree.ExpandAll();
            var widthAfter = _connectionTree.Columns[0].Width;

            Assert.That(widthAfter, Is.GreaterThan(widthBefore));
	    }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void RenamingNodeWithNothingSelectedDoesNothing()
	    {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
	        connectionTreeModel.AddRootNode(root);

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
	        _connectionTree.ExpandAll();
			_connectionTree.SelectedObject = null;

	        Assert.DoesNotThrow(() => _connectionTree.RenameSelectedNode());
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void CopyHostnameCopiesTheHostnameOfTheSelectedConnection()
        {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
			var con1 = new ConnectionInfo {Hostname = "MyHost"};
			root.AddChild(con1);
			connectionTreeModel.AddRootNode(root);

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
			_connectionTree.ExpandAll();
	        _connectionTree.SelectedObject = con1;

	        var clipboard = Substitute.For<IClipboard>();
			_connectionTree.CopyHostnameSelectedNode(clipboard);
			clipboard.Received(1).SetText(con1.Hostname);
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void CopyHostnameCopiesTheNodeNameOfTheSelectedContainer()
        {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
	        var container = new ContainerInfo { Name = "MyFolder" };
	        root.AddChild(container);
	        connectionTreeModel.AddRootNode(root);

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
	        _connectionTree.ExpandAll();
			_connectionTree.SelectedObject = container;

	        var clipboard = Substitute.For<IClipboard>();
			_connectionTree.CopyHostnameSelectedNode(clipboard);
			clipboard.Received(1).SetText(container.Name);
		}

        [Test]
        [Apartment(ApartmentState.STA)]
        public void CopyHostnameDoesNotCopyAnythingIfNoNodeSelected()
        {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
	        var con1 = new ConnectionInfo { Hostname = "MyHost" };
	        root.AddChild(con1);
	        connectionTreeModel.AddRootNode(root);

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
	        _connectionTree.ExpandAll();
			_connectionTree.SelectedObject = null;

			var clipboard = Substitute.For<IClipboard>();
			_connectionTree.CopyHostnameSelectedNode(clipboard);
			clipboard.DidNotReceiveWithAnyArgs().SetText("");
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void CopyHostnameDoesNotCopyAnythingIfHostnameOfSelectedConnectionIsEmpty()
        {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
	        var con1 = new ConnectionInfo { Hostname = string.Empty };
	        root.AddChild(con1);
	        connectionTreeModel.AddRootNode(root);

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
	        _connectionTree.ExpandAll();
			_connectionTree.SelectedObject = con1;

	        var clipboard = Substitute.For<IClipboard>();
			_connectionTree.CopyHostnameSelectedNode(clipboard);
			clipboard.DidNotReceiveWithAnyArgs().SetText("");
		}

        [Test]
        [Apartment(ApartmentState.STA)]
        public void CopyHostnameDoesNotCopyAnythingIfNameOfSelectedContainerIsEmpty()
        {
	        var connectionTreeModel = new ConnectionTreeModel();
	        var root = new RootNodeInfo(RootNodeType.Connection);
	        var con1 = new ContainerInfo { Name = string.Empty};
	        root.AddChild(con1);
	        connectionTreeModel.AddRootNode(root);

	        _connectionTree.ConnectionTreeModel = connectionTreeModel;
	        _connectionTree.ExpandAll();
			_connectionTree.SelectedObject = con1;

	        var clipboard = Substitute.For<IClipboard>();
			_connectionTree.CopyHostnameSelectedNode(clipboard);
			clipboard.DidNotReceiveWithAnyArgs().SetText("");
		}
	}
}
