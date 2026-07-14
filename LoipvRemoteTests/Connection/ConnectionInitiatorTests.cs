using System;
using System.Linq;
using LoipvRemote.App;
using LoipvRemote.App.Composition;
using LoipvRemote.Connection;
using LoipvRemote.Connection.Protocol;
using LoipvRemote.Config.Putty;
using LoipvRemote.UseCases.Configuration;
using LoipvRemoteTests.TestHelpers;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.Infrastructure.Persistence;
using LoipvRemote.Infrastructure.Windows.Dpapi;
using LoipvRemote.Infrastructure.Windows.ProcessManagement;
using LoipvRemoteTests.TestHelpers;
using LoipvRemote.Messages;
using LoipvRemote.Tools;
using LoipvRemote.UI.Adapters;
using LoipvRemote.UI.Panels;
using LoipvRemote.UseCases.Configuration;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection
{
    [TestFixture]
    public class ConnectionInitiatorTests
    {
        private ConnectionInitiator _connectionInitiator;
        private MessageCollector _messageCollector;

        [SetUp]
        public void Setup()
        {
            _messageCollector = Runtime.MessageCollector;
            RuntimeState runtimeState = new() { WindowList = [] };
            ConnectionWorkspaceAdapter connectionWorkspace = new();
            ExternalToolsService externalToolsService = new();
            _connectionInitiator = new ConnectionInitiator(
                new ProtocolFactory(new ExternalCredentialConnectorRegistry([]), TestSecretStore.Instance, _messageCollector, connectionWorkspace, externalToolsService, new WindowsExternalApplicationHostFactory()),
                externalToolsService,
                runtimeState,
                _messageCollector,
                connectionWorkspace,
                new ConnectionsService(
                    PuttySessionsManager.Instance,
                    new ConnectionStoreRuntime(
                        new ConnectionDefinitionPersistenceRuntime(new ConnectionStoreConfigurationService(new ConnectionDefinitionStoreFactory())),
                        new XmlConnectionStoreOptionsProvider(),
                        new DpapiStringSecretStore(new WindowsDpapiSecretProtector())),
                    _messageCollector),
                new PanelAdder(runtimeState, _messageCollector, connectionWorkspace));
            _messageCollector.ClearMessages();
        }

        [TearDown]
        public void Teardown()
        {
            _messageCollector?.ClearMessages();
        }

        [Test]
        public void OpenConnection_WithEmptyHostname_AddsErrorMessage()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Name = "Test Connection",
                Hostname = "", // Empty hostname
                Protocol = ProtocolType.RDP // RDP doesn't support blank hostname
            };

            // Act
            _connectionInitiator.OpenConnection(connectionInfo);

            // Assert - poll for message with timeout
            var foundMessage = WaitForMessage(MessageClass.ErrorMsg, timeoutMs: 1000);
            Assert.That(foundMessage, Is.Not.Null, "Expected an error message to be added");
            Assert.That(foundMessage.Text, Is.EqualTo("Cannot open connection: No hostname specified!"));
        }

        [Test]
        public void OpenConnection_WithNullHostname_AddsErrorMessage()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Name = "Test Connection",
                Hostname = null, // Null hostname
                Protocol = ProtocolType.SSH2 // SSH doesn't support blank hostname
            };

            // Act
            _connectionInitiator.OpenConnection(connectionInfo);

            // Assert - poll for message with timeout
            var foundMessage = WaitForMessage(MessageClass.ErrorMsg, timeoutMs: 1000);
            Assert.That(foundMessage, Is.Not.Null, "Expected an error message to be added");
            Assert.That(foundMessage.Text, Is.EqualTo("Cannot open connection: No hostname specified!"));
        }

        [Test]
        public void OpenConnection_WithValidHostname_DoesNotAddHostnameError()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Name = "Test Connection",
                Hostname = "192.168.1.1", // Valid hostname
                Protocol = ProtocolType.RDP
            };

            // Act
            _connectionInitiator.OpenConnection(connectionInfo);

            // Give a moment for any potential async operations
            System.Threading.Thread.Sleep(200);

            // Assert
            var hostnameErrors = _messageCollector.Messages
                .Where(m => m.Text == "Cannot open connection: No hostname specified!")
                .ToList();

            Assert.That(hostnameErrors, Is.Empty,
                "Should not have hostname error when hostname is provided");
        }

        /// <summary>
        /// Polls the message collector for a message of the specified class
        /// </summary>
        private IMessage WaitForMessage(MessageClass messageClass, int timeoutMs = 1000)
        {
            var startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                var message = _messageCollector.Messages
                    .FirstOrDefault(m => m.Class == messageClass);

                if (message != null)
                    return message;

                System.Threading.Thread.Sleep(50); // Poll every 50ms
            }
            return null;
        }
    }
}
