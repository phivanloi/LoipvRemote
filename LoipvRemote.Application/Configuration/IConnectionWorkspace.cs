namespace LoipvRemote.UseCases.Configuration;

/// <summary>
/// Application boundary for the active connection workspace.
/// The desktop adapter owns the tree implementation; application workflows
/// only need lifecycle and persistence operations exposed here.
/// </summary>
public interface IConnectionWorkspace
{
    bool IsConnectionsFileLoaded { get; }

    bool UsingDatabase { get; }

    string ConnectionFileName { get; }

    DateTime LastSqlUpdate { get; set; }

    DateTime LastFileUpdate { get; set; }

    string GetStartupConnectionFileName();

    string GetDatabaseRevision();

    void NewConnectionsFile(string filename);

    void LoadConnections(bool useDatabase, bool import, string connectionFileName);

    void SaveConnections();

    void SaveConnectionsAsync(string propertyNameTrigger = "");

    IDisposable BatchedSavingContext();

    void BeginBatchingSaves();

    void EndBatchingSaves();

    void DisableRemoteSynchronization();

    void EnableRemoteSynchronization();
}
