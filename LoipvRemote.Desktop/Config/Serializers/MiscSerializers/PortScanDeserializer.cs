using System.Collections.Generic;
using System.Runtime.Versioning;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Tools;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;

namespace LoipvRemote.Config.Serializers.MiscSerializers
{
    [SupportedOSPlatform("windows")]
    public class PortScanDeserializer(ProtocolKind targetProtocolType) : IDeserializer<IEnumerable<ScanHost>, ConnectionTreeModel>
    {
        private readonly ProtocolKind _targetProtocolType = targetProtocolType;

        public ConnectionTreeModel Deserialize(IEnumerable<ScanHost> scannedHosts)
        {
            ConnectionTreeModel connectionTreeModel = new();
            RootNodeInfo root = new(RootNodeType.Connection);
            connectionTreeModel.AddRootNode(root);

            foreach (ScanHost host in scannedHosts)
                ImportScannedHost(host, root);

            return connectionTreeModel;
        }

        private void ImportScannedHost(ScanHost host, ContainerInfo parentContainer)
        {
            ProtocolKind finalProtocol = default(ProtocolKind);
            bool protocolValid = true;

            switch (_targetProtocolType)
            {
                case ProtocolKind.Ssh2:
                    if (host.Ssh)
                        finalProtocol = ProtocolKind.Ssh2;
                    break;
                case ProtocolKind.Telnet:
                    if (host.Telnet)
                        finalProtocol = ProtocolKind.Telnet;
                    break;
                case ProtocolKind.Http:
                    if (host.Http)
                        finalProtocol = ProtocolKind.Http;
                    break;
                case ProtocolKind.Https:
                    if (host.Https)
                        finalProtocol = ProtocolKind.Https;
                    break;
                case ProtocolKind.Rlogin:
                    if (host.Rlogin)
                        finalProtocol = ProtocolKind.Rlogin;
                    break;
                case ProtocolKind.Rdp:
                    if (host.Rdp)
                        finalProtocol = ProtocolKind.Rdp;
                    break;
                case ProtocolKind.Vnc:
                    if (host.Vnc)
                        finalProtocol = ProtocolKind.Vnc;
                    break;
                case ProtocolKind.Ard:
                    if (host.Vnc)
                        finalProtocol = ProtocolKind.Ard;
                    break;
                default:
                    protocolValid = false;
                    break;
            }

            if (!protocolValid) return;
            ConnectionInfo newConnectionInfo = new()
            {
                Name = host.HostNameWithoutDomain,
                Hostname = host.HostName,
                Protocol = finalProtocol
            };
            newConnectionInfo.SetDefaultPort();

            parentContainer.AddChild(newConnectionInfo);
        }
    }
}