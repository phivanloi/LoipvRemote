using System;
using System.Linq;
using LoipvRemote.App;
using LoipvRemote.App.Composition;
using LoipvRemote.Connection;
using LoipvRemote.UseCases.Configuration;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.UseCases.Sessions;
using NSubstitute;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemoteTests.TestHelpers;
using LoipvRemote.Messages;
using LoipvRemote.Tools;
using LoipvRemote.UI.Adapters;
using LoipvRemote.UI.Panels;
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
            _messageCollector = new MessageCollector();
            RuntimeState runtimeState = new() { WindowList = [] };
            ConnectionWorkspaceAdapter connectionWorkspace = new();
            ExternalToolsService externalToolsService = new();
            IProtocolFactory protocolFactory = Substitute.For<IProtocolFactory>();
            SessionLifecycleCoordinator lifecycleCoordinator = new();
            _connectionInitiator = new ConnectionInitiator(
                protocolFactory,
                externalToolsService,
                runtimeState,
                null,
                _messageCollector,
                connectionWorkspace,
                _ => null,
                new PanelAdder(runtimeState, _messageCollector, connectionWorkspace),
                new ConnectionSessionOrchestrator(protocolFactory, lifecycleCoordinator),
                lifecycleCoordinator);
            _messageCollector.ClearMessages();
        }

        [TearDown]
        public void Teardown()
        {
            _messageCollector?.ClearMessages();
        }

        [Test]
        public async Task OpenConnection_WithEmptyHostname_AddsErrorMessage()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Name = "Test Connection",
                Hostname = "", // Empty hostname
                Protocol = ProtocolKind.Rdp // RDP doesn't support blank hostname
            };

            // Act
            await _connectionInitiator.OpenConnectionAsync(connectionInfo);

            // Assert - poll for message with timeout
            var foundMessage = WaitForMessage(MessageClass.ErrorMsg, timeoutMs: 1000);
            Assert.That(foundMessage, Is.Not.Null, "Expected an error message to be added");
            Assert.That(foundMessage.Text, Is.EqualTo("Cannot open connection: No hostname specified!"));
        }

        [Test]
        public async Task OpenConnection_WithNullHostname_AddsErrorMessage()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Name = "Test Connection",
                Hostname = null, // Null hostname
                Protocol = ProtocolKind.Ssh2 // SSH doesn't support blank hostname
            };

            // Act
            await _connectionInitiator.OpenConnectionAsync(connectionInfo);

            // Assert - poll for message with timeout
            var foundMessage = WaitForMessage(MessageClass.ErrorMsg, timeoutMs: 1000);
            Assert.That(foundMessage, Is.Not.Null, "Expected an error message to be added");
            Assert.That(foundMessage.Text, Is.EqualTo("Cannot open connection: No hostname specified!"));
        }

        [Test]
        public async Task OpenConnection_WithValidHostname_DoesNotAddHostnameError()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Name = "Test Connection",
                Hostname = "192.168.1.1", // Valid hostname
                Protocol = ProtocolKind.Rdp
            };

            // Act
            await _connectionInitiator.OpenConnectionAsync(connectionInfo);

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
                    .FirstOrDefault(m => m.MessageClass == messageClass);

                if (message != null)
                    return message;

                System.Threading.Thread.Sleep(50); // Poll every 50ms
            }
            return null;
        }
    }
}
