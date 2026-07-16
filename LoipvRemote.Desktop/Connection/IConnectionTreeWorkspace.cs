using LoipvRemote.Config.Connections;
using LoipvRemote.Config.Connections.Multiuser;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Container;
using LoipvRemote.Security;
using LoipvRemote.Tree;

namespace LoipvRemote.Connection;

/// <summary>
/// Desktop adapter contract for tree and lifecycle notifications.
/// This is a Desktop-only runtime contract: it owns the WinForms tree and its lifecycle notifications.
/// </summary>
public interface IConnectionTreeWorkspace
{
    bool IsConnectionsFileLoaded { get; }

    bool UsingDatabase { get; }

    string ConnectionFileName { get; }

    DateTime LastSqlUpdate { get; set; }

    DateTime LastFileUpdate { get; set; }

    ConnectionTreeModel ConnectionTreeModel { get; }
    RemoteConnectionsSyncronizer? RemoteConnectionsSyncronizer { get; set; }

    event EventHandler<ConnectionsLoadedEventArgs>? ConnectionsLoaded;
    event EventHandler<ConnectionsSavedEvent>? ConnectionsSaved;

    ConnectionInfo? CreateQuickConnect(string connectionString, ProtocolKind protocol);

    string GetStartupConnectionFileName();

    Task<string> GetDatabaseRevisionAsync(CancellationToken cancellationToken = default);

    Task NewConnectionsFileAsync(string filename, CancellationToken cancellationToken = default);

    Task LoadConnectionsAsync(bool useDatabase, bool import, string connectionFileName, CancellationToken cancellationToken = default);

    Task SaveConnectionsAsync(CancellationToken cancellationToken = default);

    void RequestSave(string propertyNameTrigger = "");

    IDisposable BatchedSavingContext();

    void BeginBatchingSaves();

    void EndBatchingSaves();

    void DisableRemoteSynchronization();

    void EnableRemoteSynchronization();

    Task SaveConnectionsAsync(
        ConnectionTreeModel connectionTreeModel,
        bool useDatabase,
        SaveFilter saveFilter,
        string connectionFileName,
        bool forceSave = false,
        string propertyNameTrigger = "",
        CancellationToken cancellationToken = default);
}
