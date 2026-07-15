using System;
using System.Collections.Generic;
using System.Security;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers;
using LoipvRemote.Credential;


namespace LoipvRemote.Config
{
    public class CredentialRecordSaver
    {
        private readonly IDataProvider<string> _dataProvider;
        private readonly ISecureSerializer<IEnumerable<ICredentialRecord>, string> _serializer;

        public CredentialRecordSaver(IDataProvider<string> dataProvider, ISecureSerializer<IEnumerable<ICredentialRecord>, string> serializer)
        {
            ArgumentNullException.ThrowIfNull(dataProvider);
            ArgumentNullException.ThrowIfNull(serializer);

            _dataProvider = dataProvider;
            _serializer = serializer;
        }

        public void Save(IEnumerable<ICredentialRecord> credentialRecords, SecureString key)
        {
            string serializedCredentials = _serializer.Serialize(credentialRecords, key);
            _dataProvider.Save(serializedCredentials);
        }
    }
}