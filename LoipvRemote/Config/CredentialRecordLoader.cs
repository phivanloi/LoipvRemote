using System;
using System.Collections.Generic;
using System.Security;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers;
using LoipvRemote.Credential;


namespace LoipvRemote.Config
{
    public class CredentialRecordLoader
    {
        private readonly IDataProvider<string> _dataProvider;
        private readonly ISecureDeserializer<string, IEnumerable<ICredentialRecord>> _deserializer;

        public CredentialRecordLoader(IDataProvider<string> dataProvider,
                                      ISecureDeserializer<string, IEnumerable<ICredentialRecord>> deserializer)
        {
            if (dataProvider == null)
                throw new ArgumentNullException(nameof(dataProvider));
            if (deserializer == null)
                throw new ArgumentNullException(nameof(deserializer));

            _dataProvider = dataProvider;
            _deserializer = deserializer;
        }

        public IEnumerable<ICredentialRecord> Load(SecureString key)
        {
            string serializedCredentials = _dataProvider.Load();
            return _deserializer.Deserialize(serializedCredentials, key);
        }
    }
}