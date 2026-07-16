using System.Collections.Generic;
using LoipvRemote.Container;
using LoipvRemote.UI.Window;

namespace LoipvRemote.Connection
{
    public interface IConnectionInitiator
    {
        IEnumerable<string> ActiveConnections { get; }

        Task OpenConnectionAsync(
            ContainerInfo containerInfo,
            ConnectionInfo.Force force = ConnectionInfo.Force.None,
            ConnectionWindow? conForm = null);

        Task OpenConnectionAsync(
            ConnectionInfo connectionInfo,
            ConnectionInfo.Force force = ConnectionInfo.Force.None,
            ConnectionWindow? conForm = null);

        bool SwitchToOpenConnection(ConnectionInfo connectionInfo);
    }
}
