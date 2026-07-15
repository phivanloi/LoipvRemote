using LoipvRemote.Config.Connections;
using LoipvRemote.Config.Connections.Multiuser;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Container;
using LoipvRemote.Security;
using LoipvRemote.Tree;
using LoipvRemote.UseCases.Configuration;

namespace LoipvRemote.Connection;

/// <summary>
/// Desktop adapter contract for tree and lifecycle notifications.
/// Persistence-only workflows must depend on <see cref="IConnectionWorkspace"/> instead.
/// </summary>
public interface IConnectionTreeWorkspace : IConnectionWorkspace
{
    ConnectionTreeModel ConnectionTreeModel { get; }
    RemoteConnectionsSyncronizer? RemoteConnectionsSyncronizer { get; set; }

    event EventHandler<ConnectionsLoadedEventArgs>? ConnectionsLoaded;
    event EventHandler<ConnectionsSavedEventArgs>? ConnectionsSaved;

    ConnectionInfo? CreateQuickConnect(string connectionString, ProtocolKind protocol);

    void SaveConnections(
        ConnectionTreeModel connectionTreeModel,
        bool useDatabase,
        SaveFilter saveFilter,
        string connectionFileName,
        bool forceSave = false,
        string propertyNameTrigger = "");
}
