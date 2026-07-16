using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;

namespace LoipvRemote.Credential.Repositories
{
    public class CompositeRepositoryUnlocker
    {
        private readonly List<ICredentialRepository> _repositories = [];

        public IEnumerable<ICredentialRepository> Repositories => _repositories;
        public ICredentialRepository? SelectedRepository { get; set; }

        public CompositeRepositoryUnlocker(IEnumerable<ICredentialRepository> repositories)
        {
            ArgumentNullException.ThrowIfNull(repositories);

            _repositories.AddRange(repositories);
            SelectNextLockedRepository();
        }

        public void Unlock(SecureString key)
        {
            SelectedRepository?.LoadCredentials(key);
        }

        public void SelectNextLockedRepository()
        {
            SelectedRepository = GetNextLockedRepo();
        }

        private ICredentialRepository? GetNextLockedRepo()
        {
            IList<ICredentialRepository> newOrder = OrderListForNextLockedRepo();
            return newOrder.Any() ? newOrder.First() : null;
        }

        private List<ICredentialRepository> OrderListForNextLockedRepo()
        {
            if (_repositories.Count == 0)
                return new List<ICredentialRepository>();
            List<ICredentialRepository> reorderedList = new();
            List<ICredentialRepository> itemsAfterCurrent = BuildListOfItemsAfterCurrent();
            List<ICredentialRepository> itemsBeforeAndIncludingCurrent = BuildListOfItemsBeforeAndIncludingCurrent();
            reorderedList.AddRange(itemsAfterCurrent.Where(repository => !repository.IsLoaded));
            reorderedList.AddRange(itemsBeforeAndIncludingCurrent.Where(repository => !repository.IsLoaded));
            return reorderedList;
        }

        private List<ICredentialRepository> BuildListOfItemsAfterCurrent()
        {
            int lastListIndex = _repositories.Count - 1;
            int newListStartIndex = GetNewListStartIndex();

            if (newListStartIndex > lastListIndex) newListStartIndex--;
            int countToEndOfList = _repositories.Count - newListStartIndex;
            return _repositories.GetRange(newListStartIndex, countToEndOfList);
        }

        private List<ICredentialRepository> BuildListOfItemsBeforeAndIncludingCurrent()
        {
            int newListStartIndex = GetNewListStartIndex();
            return _repositories.GetRange(0, newListStartIndex);
        }

        private int GetNewListStartIndex()
        {
            // IndexOf of a null selection is -1; guard so the non-null IndexOf overload is satisfied.
            int currentItemIndex = SelectedRepository is null ? -1 : _repositories.IndexOf(SelectedRepository);
            return currentItemIndex + 1;
        }
    }
}
