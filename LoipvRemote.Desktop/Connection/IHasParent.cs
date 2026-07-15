using LoipvRemote.Container;

namespace LoipvRemote.Connection
{
    public interface IHasParent
    {
        ContainerInfo Parent { get; }

        void SetParent(ContainerInfo containerInfo);

        void RemoveParent();
    }
}