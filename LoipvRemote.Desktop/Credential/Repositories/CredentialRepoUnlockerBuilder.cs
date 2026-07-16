using System.Collections.Generic;

namespace LoipvRemote.Credential.Repositories
{
    public class CredentialRepoUnlockerBuilder
    {
        public static CompositeRepositoryUnlocker Build(IEnumerable<ICredentialRepository> repos)
        {
            return new CompositeRepositoryUnlocker(repos);
        }
    }
}