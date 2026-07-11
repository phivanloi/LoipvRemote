using LoipvRemote.Tree;

namespace LoipvRemote.Config.Connections
{
    public interface IConnectionsLoader
    {
        ConnectionTreeModel Load();
    }
}