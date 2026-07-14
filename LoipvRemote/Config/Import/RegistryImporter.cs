using System;
using System.Runtime.Versioning;
using LoipvRemote.Container;
using LoipvRemote.Infrastructure.Windows.Registry;
using LoipvRemote.Messages;

namespace LoipvRemote.Config.Import
{
    [SupportedOSPlatform("windows")]
    internal sealed class RegistryImporter(MessageCollector messageCollector) : IConnectionImporter<string>
    {
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));

        public void Import(string regPath, ContainerInfo destinationContainer)
        {
            ImportFromRegistry(regPath, destinationContainer);
        }

        public void ImportFromRegistry(string regPath, ContainerInfo destinationContainer)
        {
            try
            {
                ContainerInfo importedNode = new()
                {
                    Name = "Imported from PuTTY",
                    IsContainer = true
                };

                PuttyRegistrySessionStore store = new();
                foreach (PuttyRegistrySession session in store.GetSessions())
                {
                    Connection.Protocol.ProtocolType protocol = session.Protocol.Equals("raw", StringComparison.OrdinalIgnoreCase)
                        ? Connection.Protocol.ProtocolType.RAW
                        : Connection.Protocol.ProtocolType.SSH2;
                    importedNode.AddChild(new Connection.ConnectionInfo
                    {
                        Name = session.Name,
                        Hostname = session.Hostname,
                        Port = session.Port == 0 ? 22 : session.Port,
                        Protocol = protocol,
                        Parent = destinationContainer,
                        Username = session.Username
                    });
                }

                destinationContainer.AddChild(importedNode);
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("Config.Import.Registry.Import() failed.", ex);
            }
        }

    }
}
