using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Security;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;
using LoipvRemote.Messages;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Xml;

namespace LoipvRemote.Config.Serializers.MiscSerializers
{
    [SupportedOSPlatform("windows")]
    public sealed class SecureCRTFileDeserializer(MessageCollector messageCollector)
    {
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
        enum SecureCRTNodeType { folder, session };

        public ConnectionTreeModel Deserialize(string content)
        {
            ConnectionTreeModel connectionTreeModel = new();
            RootNodeInfo root = new(RootNodeType.Connection);
            connectionTreeModel.AddRootNode(root);

            XmlDocument xmlDocument = SecureXmlHelper.LoadXmlFromString(content);

            XmlNode sessionsNode = RequireNode(
                xmlDocument.SelectSingleNode("/VanDyke/key[@name=\"Sessions\"]"),
                "Sessions");

            ImportRootOrContainer(sessionsNode, root);

            return connectionTreeModel;
        }

        private void ImportRootOrContainer(XmlNode rootNode, ContainerInfo parentContainer)
        {
            ContainerInfo newContainer = ImportContainer(rootNode, parentContainer);

            if (rootNode.ChildNodes.Count == 0)
                return;

            foreach (XmlNode child in rootNode.ChildNodes)
            {
                string name = child.Attributes?["name"]?.Value
                    ?? throw new FileFormatException("SecureCRT node is missing its name attribute.");
                if (name == "Default" || name == "Default_LocalShell")
                    continue;
                SecureCRTNodeType nodeType = GetFolderOrSession(child);
                switch (nodeType)
                {
                    case SecureCRTNodeType.folder:
                        ImportRootOrContainer(child, newContainer);
                        break;
                    case SecureCRTNodeType.session:
                        ImportConnection(child, newContainer);
                        break;
                }
            }
        }

        private void ImportConnection(XmlNode childNode, ContainerInfo parentContainer)
        {
            ConnectionInfo? connectionInfo = ConnectionInfoFromXml(childNode);
            if (connectionInfo is null)
                return;

            parentContainer.AddChild(connectionInfo);
        }

        private static ContainerInfo ImportContainer(XmlNode containerNode, ContainerInfo parentContainer)
        {
            ContainerInfo containerInfo = new()
            {
                Name = containerNode.Attributes?["name"]?.InnerText
                    ?? throw new FileFormatException("SecureCRT container is missing its name attribute.")
            };
            parentContainer.AddChild(containerInfo);
            return containerInfo;
        }

        private static SecureCRTNodeType GetFolderOrSession(XmlNode xmlNode)
        {
            if (GetHostnameFromNode(xmlNode) == null)
                return SecureCRTNodeType.folder;

            return SecureCRTNodeType.session;
        }

        private ConnectionInfo? ConnectionInfoFromXml(XmlNode xmlNode)
        {
            ConnectionInfo connectionInfo = new();
            try
            {
                connectionInfo.Name = xmlNode.Attributes?["name"]?.InnerText
                    ?? throw new FileFormatException("SecureCRT session is missing its name attribute.");
                connectionInfo.Hostname = GetHostnameFromNode(xmlNode) ?? string.Empty;
                connectionInfo.Protocol = GetProtocolFromNode(xmlNode);
                connectionInfo.Port = GetPortFromNode(xmlNode, connectionInfo.Protocol);
                connectionInfo.Username = GetUsernameFromNode(xmlNode) ?? string.Empty;
                connectionInfo.Description = GetDescriptionFromNode(xmlNode);
            }
            catch (FileFormatException e)
            {
                _messageCollector.AddExceptionMessage("Error when parsing SecureCRT node: ", e);
                return null;
            }

            return connectionInfo;
        }

        private static string? GetHostnameFromNode(XmlNode xmlNode)
        {
            return xmlNode.SelectSingleNode("string[@name=\"Hostname\"]")?.InnerText;

        }

        private static string? GetUsernameFromNode(XmlNode xmlNode)
        {
            return xmlNode.SelectSingleNode("string[@name=\"Username\"]")?.InnerText;
        }

        private static int GetPortFromNode(XmlNode xmlNode, ProtocolKind protocol)
        {
            switch (protocol)
            {
                case ProtocolKind.Ssh1:
                    return ParsePort(xmlNode.SelectSingleNode("dword[@name=\"[SSH1] Port\"]"));
                case ProtocolKind.Ssh2:
                    return ParsePort(xmlNode.SelectSingleNode("dword[@name=\"[SSH2] Port\"]"));
                default:
                    return ParsePort(xmlNode.SelectSingleNode("dword[@name=\"Port\"]"));
            }
        }

        private static int ParsePort(XmlNode? portNode)
        {
            // SecureCRT omits a port for protocols that use their default endpoint
            // (for example Rlogin). Preserve that protocol default as port 0.
            if (portNode is null)
                return 0;

            string value = portNode.InnerText;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
                throw new FileFormatException($"Invalid SecureCRT port '{value}'.");

            return port;
        }

        private static ProtocolKind GetProtocolFromNode(XmlNode xmlNode)
        {
            XmlNode? protocolNode = xmlNode.SelectSingleNode("string[@name=\"Protocol Name\"]");
            if (protocolNode == null)
                throw new FileFormatException($"Protocol node not found");

            string protocolText = protocolNode.InnerText.ToUpperInvariant();
            switch (protocolText)
            {
                case "RDP":
                    return ProtocolKind.Rdp;
                case "RAW":
                    return ProtocolKind.Raw;
                case "RLOGIN":
                    return ProtocolKind.Rlogin;
                case "SSH1":
                    return ProtocolKind.Ssh1;
                case "SSH2":
                    return ProtocolKind.Ssh2;
                case "TELNET":
                    return ProtocolKind.Telnet;
                default:
                    throw new FileFormatException($"Unrecognized protocol ({protocolText}).");
            }
        }

        private static string GetDescriptionFromNode(XmlNode xmlNode)
        {
            string description = string.Empty;
            XmlNode? descNode = xmlNode.SelectSingleNode("array[@name=\"Description\"]");
            if (descNode == null)
                return string.Empty;

            foreach (XmlNode n in descNode.ChildNodes)
            {
                description += n.InnerText + " ";
            }

            return description.TrimEnd();
        }

        private static XmlNode RequireNode(XmlNode? node, string description) =>
            node ?? throw new FileFormatException($"SecureCRT {description} node was not found.");
    }
}
