using LoipvRemote.Connection;
using LoipvRemote.Messages;

namespace LoipvRemote.Config.Connections.Multiuser;

/// <summary>Polls the configured Domain store without relying on the removed tblUpdate schema.</summary>
public sealed class ConnectionStoreUpdateChecker(IConnectionTreeWorkspace workspace, MessageCollector messageCollector) : IConnectionsUpdateChecker
{
    private readonly IConnectionTreeWorkspace _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
    private string? _knownRevision;
    private int _disposed;

    public async Task<bool> IsUpdateAvailableAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        UpdateCheckStarted?.Invoke(this, EventArgs.Empty);
        bool updateAvailable = false;
        try
        {
            string revision = await _workspace.GetDatabaseRevisionAsync(cancellationToken).ConfigureAwait(false);
            updateAvailable = _knownRevision is not null && !string.Equals(_knownRevision, revision, StringComparison.Ordinal);
            _knownRevision = revision;
            if (updateAvailable)
                ConnectionsUpdateAvailable?.Invoke(this, new ConnectionsUpdateAvailableEventArgs(revision));
        }
        catch (Exception exception)
        {
            _messageCollector.AddMessage(MessageClass.WarningMsg,
                $"Unable to poll the connection definition store for updates.{Environment.NewLine}{exception.Message}", true);
        }
        finally
        {
            UpdateCheckFinished?.Invoke(this, new ConnectionsUpdateCheckFinishedEventArgs { UpdateAvailable = updateAvailable });
        }

        return updateAvailable;
    }

    public event EventHandler? UpdateCheckStarted;
    public event EventHandler<ConnectionsUpdateCheckFinishedEventArgs>? UpdateCheckFinished;
    public event EventHandler<ConnectionsUpdateAvailableEventArgs>? ConnectionsUpdateAvailable;

    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }
}
