using System;
using System.Collections.Generic;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers.CredentialProviderSerializer;
using LoipvRemote.Credential;

namespace LoipvRemote.Config
{
    public class CredentialRepositoryListSaver : ISaver<IEnumerable<ICredentialRepository>>
    {
        private readonly IDataProvider<string> _dataProvider;

        public CredentialRepositoryListSaver(IDataProvider<string> dataProvider)
        {
            ArgumentNullException.ThrowIfNull(dataProvider);

            _dataProvider = dataProvider;
        }

        public void Save(IEnumerable<ICredentialRepository> repositories, string propertyNameTrigger = "")
        {
            CredentialRepositoryListSerializer serializer = new();
            string data = CredentialRepositoryListSerializer.Serialize(repositories);
            _dataProvider.Save(data);
        }
    }
}