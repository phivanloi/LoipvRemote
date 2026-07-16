using System;

namespace LoipvRemote.Config.Connections.Multiuser
{
    public interface IConnectionsUpdateChecker : IDisposable
    {
        Task<bool> IsUpdateAvailableAsync(CancellationToken cancellationToken = default);

        event EventHandler UpdateCheckStarted;
        event EventHandler<ConnectionsUpdateCheckFinishedEventArgs> UpdateCheckFinished;
        event EventHandler<ConnectionsUpdateAvailableEventArgs> ConnectionsUpdateAvailable;
    }
}
