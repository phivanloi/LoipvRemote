using System;
using LoipvRemote.Connection;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;
using LoipvRemote.UI.Controls.ConnectionTree;
using NSubstitute;
using NUnit.Framework;


namespace LoipvRemoteTests.Tree
{
    public class PreviousSessionOpenerTests
    {
        private PreviousSessionOpener _previousSessionOpener;
        private IConnectionInitiator _connectionInitiator;
        private IConnectionTree _connectionTree;

        [SetUp]
        public void Setup()
        {
            _connectionInitiator = Substitute.For<IConnectionInitiator>();
            _previousSessionOpener = new PreviousSessionOpener(_connectionInitiator);
            _connectionTree = Substitute.For<IConnectionTree>();
            _connectionTree.GetRootConnectionNode().Returns(BuildTree());
        }

        [Test]
        public void AllRequestedSessionsAreReopened()
        {
            _previousSessionOpener.Execute(_connectionTree);
            _ = _connectionInitiator.ReceivedWithAnyArgs(2).OpenConnectionAsync(new ConnectionInfo());
        }

        [Test]
        public void ExceptionThrownWhenConstructorGivenNullArg()
        {
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Throws<ArgumentNullException>(() => new PreviousSessionOpener(null));
        }

        private RootNodeInfo BuildTree()
        {
            var root = new RootNodeInfo(RootNodeType.Connection);
            root.AddChild(new ConnectionInfo { PleaseConnect = true });
            root.AddChild(new ConnectionInfo());
            root.AddChild(new ConnectionInfo { PleaseConnect = true });
            return root;
        }
    }
}
