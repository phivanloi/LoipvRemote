using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.Connection;
using LoipvRemote.Security;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;

namespace LoipvRemote.Config.Serializers.ConnectionSerializers.Xml
{
    [SupportedOSPlatform("windows")]
    public class XmlConnectionSerializerFactory
    {
        public ISerializer<ConnectionInfo, string> Build(
            ICryptographyProvider cryptographyProvider,
            ConnectionTreeModel connectionTreeModel,
            SaveFilter saveFilter = null,
            bool useFullEncryption = false)
        {
            System.Security.SecureString encryptionKey = connectionTreeModel
                .RootNodes.OfType<RootNodeInfo>()
                .First().PasswordString
                .ConvertToSecureString();

            XmlConnectionNodeSerializer28 connectionNodeSerializer = new(
                cryptographyProvider,
                encryptionKey,
                saveFilter ?? new SaveFilter());

            return new XmlConnectionsSerializer(cryptographyProvider, connectionNodeSerializer)
            {
                UseFullEncryption = useFullEncryption
            };
        }
    }
}
