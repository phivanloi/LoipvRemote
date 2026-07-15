using System.Collections.Generic;
using System.Reflection;
using LoipvRemote.Connection;
using LoipvRemote.UI.Adapters;
using LoipvRemote.Container;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Tree.Root;
using NUnit.Framework;


namespace LoipvRemoteTests.Connection
{
	public class ConnectionInfoTests
    {
        private ConnectionInfo _connectionInfo;
        private const string TestDomain = "somedomain";

        [SetUp]
        public void Setup()
        {
            _connectionInfo = new ConnectionInfo();
        }

        [TearDown]
        public void Teardown()
        {
            _connectionInfo = null;
        }

        [Test]
        public void CopyCreatesMemberwiseCopy()
        {
            _connectionInfo.Domain = TestDomain;
            var secondConnection = _connectionInfo.Clone();
            Assert.That(secondConnection.Domain, Is.EqualTo(_connectionInfo.Domain));
        }

        [Test]
        public void CloneDoesNotSetParentOfNewConnectionInfo()
        {
            _connectionInfo.SetParent(new ContainerInfo());
            var clonedConnection = _connectionInfo.Clone();
            Assert.That(clonedConnection.Parent, Is.Null);
        }

        [Test]
        public void CloneAlsoCopiesInheritanceObject()
        {
            var clonedConnection = _connectionInfo.Clone();
            Assert.That(clonedConnection.Inheritance, Is.Not.EqualTo(_connectionInfo.Inheritance));
        }

        [Test]
        public void CloneCorrectlySetsParentOfInheritanceObject()
        {
			var originalConnection = new ConnectionInfo();
            var clonedConnection = originalConnection.Clone();
            Assert.That(clonedConnection.Inheritance.Parent, Is.EqualTo(clonedConnection));
        }

        [Test]
        public void CopyFromCopiesProperties()
        {
            var secondConnection = new ConnectionInfo {Domain = TestDomain};
            _connectionInfo.CopyFrom(secondConnection);
            Assert.That(_connectionInfo.Domain, Is.EqualTo(secondConnection.Domain));
        }

        [Test]
        public void CopyingAConnectionInfoAlsoCopiesItsInheritance()
        {
            _connectionInfo.Inheritance.Username = true;
            var secondConnection = new ConnectionInfo {Inheritance = {Username = false}};
            secondConnection.CopyFrom(_connectionInfo);
            Assert.That(secondConnection.Inheritance.Username, Is.True);
        }

        [Test]
        public void PropertyChangedEventRaisedWhenOpenConnectionsChanges()
        {
            var eventWasCalled = false;
            _connectionInfo.PropertyChanged += (sender, args) => eventWasCalled = true;
            _connectionInfo.OpenConnections.Add(CreateProtocol());
            Assert.That(eventWasCalled);
        }

        [Test]
        public void PropertyChangedEventArgsAreCorrectWhenOpenConnectionsChanges()
        {
            var nameOfModifiedProperty = "";
            _connectionInfo.PropertyChanged += (sender, args) => nameOfModifiedProperty = args.PropertyName;
            _connectionInfo.OpenConnections.Add(CreateProtocol());
            Assert.That(nameOfModifiedProperty, Is.EqualTo("OpenConnections"));
        }

	    [TestCaseSource(typeof(InheritancePropertyProvider), nameof(InheritancePropertyProvider.GetProperties))]
	    public void MovingAConnectionFromUnderRootNodeToUnderADifferentNodeEnablesInheritance(PropertyInfo property)
	    {
		    var rootNode = new RootNodeInfo(RootNodeType.Connection);
			var otherContainer = new ContainerInfo();
		    _connectionInfo.Inheritance.EverythingInherited = true;
		    _connectionInfo.SetParent(rootNode);
			_connectionInfo.SetParent(otherContainer);
		    var propertyValue = property.GetValue(_connectionInfo.Inheritance);
		    Assert.That(propertyValue, Is.True);
	    }

		[TestCase(ProtocolKind.Http, ExpectedResult = 80)]
        [TestCase(ProtocolKind.Https, ExpectedResult = 443)]
        [TestCase(ProtocolKind.ExternalApplication, ExpectedResult = 0)]
        [TestCase(ProtocolKind.Raw, ExpectedResult = 23)]
        [TestCase(ProtocolKind.Rdp, ExpectedResult = 3389)]
        [TestCase(ProtocolKind.Rlogin, ExpectedResult = 513)]
        [TestCase(ProtocolKind.Ssh1, ExpectedResult = 22)]
        [TestCase(ProtocolKind.Ssh2, ExpectedResult = 22)]
        [TestCase(ProtocolKind.Telnet, ExpectedResult = 23)]
        [TestCase(ProtocolKind.Vnc, ExpectedResult = 5900)]
        [TestCase(ProtocolKind.Ard, ExpectedResult = 5900)]
        public int GetDefaultPortReturnsCorrectPortForProtocol(ProtocolKind protocolType)
        {
            _connectionInfo.Protocol = protocolType;
            return _connectionInfo.GetDefaultPort();
        }

	    private class InheritancePropertyProvider
	    {
		    public static IEnumerable<PropertyInfo> GetProperties()
		    {
			    return new ConnectionInfoInheritance(new ConnectionInfo()).GetProperties();
		    }
	    }

        private static ProtocolSessionBridge CreateProtocol() => new(
            new ConnectionDefinition(
                Guid.NewGuid(), "test", "localhost", 22, ProtocolKind.Ssh2, CredentialReference.None),
            new TestSession());

        private sealed class TestSession : IProtocolSession
        {
            public ProtocolSessionState State { get; private set; } = ProtocolSessionState.Created;
            public ProtocolCapabilities Capabilities => ProtocolCapabilities.None;
            public bool Initialize() => true;
            public bool Connect() => true;
            public void Disconnect() { }
            public void Focus() { }
            public void Close() => State = ProtocolSessionState.Closed;
            public void Dispose() { }
        }

    }
}
