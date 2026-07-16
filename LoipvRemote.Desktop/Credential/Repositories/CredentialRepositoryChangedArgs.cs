using System;

namespace LoipvRemote.Credential.Repositories
{
    public class CredentialRepositoryChangedEventArgs : EventArgs
    {
        public ICredentialRepository Repository { get; }

        public CredentialRepositoryChangedEventArgs(ICredentialRepository repository)
        {
            ArgumentNullException.ThrowIfNull(repository);

            Repository = repository;
        }
    }
}
