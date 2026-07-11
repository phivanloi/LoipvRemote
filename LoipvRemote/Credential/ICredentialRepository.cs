using System;
using System.Collections.Generic;
using System.Security;
using LoipvRemote.Credential.Repositories;
using LoipvRemote.Tools.CustomCollections;


namespace LoipvRemote.Credential
{
    public interface ICredentialRepository
    {
        ICredentialRepositoryConfig Config { get; }
        IList<ICredentialRecord> CredentialRecords { get; }
        bool IsLoaded { get; }
        void LoadCredentials(SecureString key);
        void SaveCredentials(SecureString key);
        void UnloadCredentials();
        event EventHandler? RepositoryConfigUpdated;
        event EventHandler<CollectionUpdatedEventArgs<ICredentialRecord>>? CredentialsUpdated;
    }
}