using System;

namespace LoipvRemote.Tools.CustomCollections
{
    public interface INotifyCollectionUpdated<T>
    {
        event EventHandler<CollectionUpdatedEventArgs<T>> CollectionUpdated;
    }
}