using System;
using System.Linq;
using LoipvRemote.App;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers.ConnectionSerializers.Xml;
using LoipvRemote.Security;
using LoipvRemote.Security.Factories;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;
using LoipvRemote.Properties;
using System.Runtime.Versioning;

namespace LoipvRemote.Config.Connections
{
    [SupportedOSPlatform("windows")]
    public class XmlConnectionsSaver : ISaver<ConnectionTreeModel>
    {
        private readonly string _connectionFileName;
        private readonly SaveFilter _saveFilter;

        public XmlConnectionsSaver(string connectionFileName, SaveFilter saveFilter)
        {
            if (string.IsNullOrEmpty(connectionFileName))
                throw new ArgumentException($"Argument '{nameof(connectionFileName)}' cannot be null or empty");
            _connectionFileName = connectionFileName;
            _saveFilter = saveFilter ?? throw new ArgumentNullException(nameof(saveFilter));
        }

        public void Save(ConnectionTreeModel connectionTreeModel, string propertyNameTrigger = "")
        {
            try
            {
                ICryptographyProvider cryptographyProvider = new CryptoProviderFactoryFromSettings().Build();
                XmlConnectionSerializerFactory serializerFactory = new();

                Serializers.ISerializer<Connection.ConnectionInfo, string> xmlConnectionsSerializer = serializerFactory.Build(cryptographyProvider, connectionTreeModel, _saveFilter, Properties.OptionsSecurityPage.Default.EncryptCompleteConnectionsFile);

                RootNodeInfo rootNode = connectionTreeModel.RootNodes.OfType<RootNodeInfo>().First();
                string xml = xmlConnectionsSerializer.Serialize(rootNode);

                FileDataProviderWithRollingBackup fileDataProvider = new(_connectionFileName);
                fileDataProvider.Save(xml);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionStackTrace("SaveToXml failed", ex);
            }
        }
    }
}