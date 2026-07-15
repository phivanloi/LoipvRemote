using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Security;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;
using LoipvRemote.Messages;
using System;
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

            XmlNode sessionsNode = xmlDocument.SelectSingleNode("/VanDyke/key[@name=\"Sessions\"]");

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
                string name = child.Attributes["name"].Value;
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
            ConnectionInfo connectionInfo = ConnectionInfoFromXml(childNode);
            if (connectionInfo == null)
                return;

            parentContainer.AddChild(connectionInfo);
        }

        private ContainerInfo ImportContainer(XmlNode containerNode, ContainerInfo parentContainer)
        {
            ContainerInfo containerInfo = new()
            {
                Name = containerNode.Attributes["name"].InnerText
            };
            parentContainer.AddChild(containerInfo);
            return containerInfo;
        }

        private SecureCRTNodeType GetFolderOrSession(XmlNode xmlNode)
        {
            if (GetHostnameFromNode(xmlNode) == null)
                return SecureCRTNodeType.folder;

            return SecureCRTNodeType.session;
        }

        private ConnectionInfo ConnectionInfoFromXml(XmlNode xmlNode)
        {
            ConnectionInfo connectionInfo = new();
            try
            {
                connectionInfo.Name = xmlNode.Attributes["name"].InnerText;
                connectionInfo.Hostname = GetHostnameFromNode(xmlNode);
                connectionInfo.Protocol = GetProtocolFromNode(xmlNode);
                connectionInfo.Port = GetPortFromNode(xmlNode, connectionInfo.Protocol);
                connectionInfo.Username = GetUsernameFromNode(xmlNode);
                connectionInfo.Description = GetDescriptionFromNode(xmlNode);
            }
            catch (FileFormatException e)
            {
                _messageCollector.AddExceptionMessage("Error when parsing SecureCRT node: ", e);
                return null;
            }

            return connectionInfo;
        }

        private string GetHostnameFromNode(XmlNode xmlNode)
        {
            return xmlNode.SelectSingleNode("string[@name=\"Hostname\"]")?.InnerText;

        }

        private string GetUsernameFromNode(XmlNode xmlNode)
        {
            return xmlNode.SelectSingleNode("string[@name=\"Username\"]")?.InnerText;
        }

        private int GetPortFromNode(XmlNode xmlNode, ProtocolKind protocol)
        {
            switch (protocol)
            {
                case ProtocolKind.Ssh1:
                    return Convert.ToInt32(xmlNode.SelectSingleNode("dword[@name=\"[SSH1] Port\"]").InnerText);
                case ProtocolKind.Ssh2:
                    return Convert.ToInt32(xmlNode.SelectSingleNode("dword[@name=\"[SSH2] Port\"]").InnerText);
                default:
                    return Convert.ToInt32(xmlNode.SelectSingleNode("dword[@name=\"Port\"]")?.InnerText);
            }
        }

        private ProtocolKind GetProtocolFromNode(XmlNode xmlNode)
        {
            XmlNode protocolNode = xmlNode.SelectSingleNode("string[@name=\"Protocol Name\"]");
            if (protocolNode == null)
                throw new FileFormatException($"Protocol node not found");

            string protocolText = protocolNode.InnerText.ToUpper();
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

        private string GetDescriptionFromNode(XmlNode xmlNode)
        {
            string description = string.Empty;
            XmlNode descNode = xmlNode.SelectSingleNode("array[@name=\"Description\"]");
            foreach(XmlNode n in descNode.ChildNodes)
            {
                description += n.InnerText + " ";
            }

            return description.TrimEnd();
        }
    }
}
