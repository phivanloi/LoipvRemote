using System;
using System.Runtime.Versioning;
using LoipvRemote.App;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers.ConnectionSerializers.Csv;
using LoipvRemote.Security;
using LoipvRemote.Tree;

namespace LoipvRemote.Config.Connections
{
    [SupportedOSPlatform("windows")]
    public class CsvConnectionsSaver : ISaver<ConnectionTreeModel>
    {
        private readonly string _connectionFileName;
        private readonly SaveFilter _saveFilter;

        public CsvConnectionsSaver(string connectionFileName, SaveFilter saveFilter)
        {
            if (string.IsNullOrEmpty(connectionFileName))
                throw new ArgumentException($"Argument '{nameof(connectionFileName)}' cannot be null or empty");
            if (saveFilter == null)
                throw new ArgumentNullException(nameof(saveFilter));

            _connectionFileName = connectionFileName;
            _saveFilter = saveFilter;
        }

        public void Save(ConnectionTreeModel connectionTreeModel, string propertyNameTrigger = "")
        {
            CsvConnectionsSerializerMremotengFormat csvConnectionsSerializer =
                new(_saveFilter, Runtime.CredentialProviderCatalog);
            FileDataProvider dataProvider = new(_connectionFileName);
            string csvContent = csvConnectionsSerializer.Serialize(connectionTreeModel);
            dataProvider.Save(csvContent);
        }
    }
}