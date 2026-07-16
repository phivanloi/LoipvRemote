using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using LoipvRemote.App;
using LoipvRemote.App.Info;
using LoipvRemote.Config;
using LoipvRemote.Config.Connections;
using LoipvRemote.Config.Connections.Multiuser;
using LoipvRemote.Config.Putty;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Messages;
using LoipvRemote.Security;
using LoipvRemote.Tools;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;
using LoipvRemote.UI;
using LoipvRemote.UI.Adapters;
using LoipvRemote.Resources.Language;
using LoipvRemote.UseCases.Configuration;
using LoipvRemote.UseCases.Credentials;
using ApplicationEdition = LoipvRemote.UseCases.Hosting.ApplicationEdition;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace LoipvRemote.Connection
{
    [SupportedOSPlatform("windows")]
    public sealed class ConnectionWorkspace(
        PuttySessionsManager puttySessionsManager,
        ConnectionStoreConfigurationService configurationService,
        IConnectionStoreOptionsProvider optionsProvider,
        IStringSecretStore secretStore,
        MessageCollector messageCollector,
        Func<ConnectionInfo, ExternalApplicationDefinition?>? externalApplicationResolver = null,
        Action<bool>? reloadConnections = null) : IConnectionTreeWorkspace
    {
        private const int AsyncSaveCoalesceDelayMilliseconds = 75;
        private readonly PuttySessionsManager _puttySessionsManager = puttySessionsManager ?? throw new ArgumentNullException(nameof(puttySessionsManager));
        private readonly ConnectionStoreConfigurationService _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        private readonly IConnectionStoreOptionsProvider _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
        private readonly IStringSecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
        private readonly Func<ConnectionInfo, ExternalApplicationDefinition?>? _externalApplicationResolver = externalApplicationResolver;
        private readonly Action<bool> _reloadConnections = reloadConnections ?? (_ => { });
        private readonly AsyncSaveRequestQueue _asyncSaveRequestQueue = new();
        private bool _batchingSaves;
        private bool _saveRequested;
        private bool _saveAsyncRequested;
        private string _lastFileContentHash = string.Empty;

        public bool IsConnectionsFileLoaded { get; set; }
        public bool UsingDatabase { get; private set; }
        public string ConnectionFileName { get; private set; } = string.Empty;
        public RemoteConnectionsSyncronizer? RemoteConnectionsSyncronizer { get; set; }
        public DateTime LastSqlUpdate { get; set; }
        public DateTime LastFileUpdate { get; set; }

        public ConnectionTreeModel ConnectionTreeModel { get; private set; } = new();

        public async Task<string> GetDatabaseRevisionAsync(CancellationToken cancellationToken = default)
        {
            ConnectionDefinitionStoreOptions options = _optionsProvider.GetOptions(useDatabase: true, connectionFileName: string.Empty);
            ConnectionTreeDefinition definition = await _configurationService
                .LoadAsync(options, cancellationToken)
                .ConfigureAwait(false);

            string payload = JsonSerializer.Serialize(new
            {
                Folders = definition.Folders
                    .OrderBy(folder => folder.Id)
                    .Select(folder => new
                    {
                        folder.Id,
                        folder.Name,
                        folder.ParentFolderId,
                        folder.SortOrder,
                        folder.IsRoot,
                        Options = NormalizeOptions(folder.Options)
                    }),
                Connections = definition.Connections
                    .OrderBy(connection => connection.Id)
                    .Select(connection => new
                    {
                        connection.Id,
                        connection.Name,
                        connection.Host,
                        connection.Port,
                        connection.Protocol,
                        connection.Credential,
                        connection.GatewayCredential,
                        connection.ExternalApplication,
                        connection.ParentFolderId,
                        connection.SortOrder,
                        Options = NormalizeOptions(connection.Options)
                    })
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        }

        public async Task NewConnectionsFileAsync(string filename, CancellationToken cancellationToken = default)
        {
            try
            {
                filename.ThrowIfNullOrEmpty(nameof(filename));
                ConnectionTreeModel newConnectionsModel = new();
                newConnectionsModel.AddRootNode(new RootNodeInfo(RootNodeType.Connection));
                await SaveConnectionsAsync(newConnectionsModel, false, new SaveFilter(), filename, true, cancellationToken: cancellationToken);
                await LoadConnectionsAsync(false, false, filename, cancellationToken);
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage(Language.CouldNotCreateNewConnectionsFile, ex);
            }
        }

        public ConnectionInfo? CreateQuickConnect(string connectionString, ProtocolKind protocol)
        {
            try
            {
                UriBuilder uriBuilder = new()
                {
                    Scheme = "dummyscheme"
                };

                if (connectionString.Contains('@'))
                {
                    string[] x = connectionString.Split('@');
                    uriBuilder.UserName = x[0];
                    connectionString = x[1];
                }
                if (connectionString.Contains(':'))
                {
                    string[] x = connectionString.Split(':');
                    connectionString = x[0];
                    uriBuilder.Port = Convert.ToInt32(x[1], CultureInfo.InvariantCulture);
                }

                uriBuilder.Host = connectionString;

                ConnectionInfo newConnectionInfo = new();
                newConnectionInfo.CopyFrom(DefaultConnectionInfo.Instance);

                newConnectionInfo.Name = Properties.OptionsTabsPanelsPage.Default.IdentifyQuickConnectTabs
                    ? FormatText(Language.Quick, uriBuilder.Host)
                    : uriBuilder.Host;

                newConnectionInfo.Protocol = protocol;
                newConnectionInfo.Hostname = uriBuilder.Host;
                newConnectionInfo.Username = uriBuilder.UserName;

                if (uriBuilder.Port == -1)
                {
                    newConnectionInfo.SetDefaultPort();
                }
                else
                {
                    newConnectionInfo.Port = uriBuilder.Port;
                }

                if (string.IsNullOrEmpty(newConnectionInfo.Panel))
                    newConnectionInfo.Panel = Language.General;

                newConnectionInfo.IsQuickConnect = true;

                return newConnectionInfo;
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage(Language.QuickConnectFailed, ex);
                return null;
            }
        }

        /// <summary>
        /// Load connections from a source. <see cref="connectionFileName"/> is ignored if
        /// <see cref="useDatabase"/> is true.
        /// </summary>
        /// <param name="useDatabase"></param>
        /// <param name="import"></param>
        /// <param name="connectionFileName"></param>
        public async Task LoadConnectionsAsync(bool useDatabase, bool import, string connectionFileName, CancellationToken cancellationToken = default)
        {
            ConnectionTreeModel oldConnectionTreeModel = ConnectionTreeModel;
            bool oldIsUsingDatabaseValue = UsingDatabase;

            ConnectionTreeModel newConnectionTreeModel = await LoadFromStoreAsync(useDatabase, connectionFileName, cancellationToken);

            if (useDatabase)
                LastSqlUpdate = DateTime.Now.ToUniversalTime();

            if (newConnectionTreeModel == null)
            {
                DialogFactory.ShowLoadConnectionsFailedDialog(
                    connectionFileName,
                    "Decrypting connection file failed",
                    IsConnectionsFileLoaded,
                    this,
                    _reloadConnections,
                    _messageCollector);
                return;
            }

            IsConnectionsFileLoaded = true;
            ConnectionFileName = connectionFileName;
            Properties.OptionsConnectionsPage.Default.ConnectionFilePath = connectionFileName;
            _lastFileContentHash = !useDatabase ? ComputeFileHash(connectionFileName) : string.Empty;

            UsingDatabase = useDatabase;

            if (!import)
            {
                _puttySessionsManager.AddSessions();
                newConnectionTreeModel.RootNodes.AddRange(_puttySessionsManager.RootPuttySessionsNodes);
            }

            ConnectionTreeModel = newConnectionTreeModel;
            UpdateCustomConsPathSetting(connectionFileName);
            RaiseConnectionsLoadedEvent(oldConnectionTreeModel, newConnectionTreeModel, oldIsUsingDatabaseValue, useDatabase, connectionFileName);
            _messageCollector.AddMessage(MessageClass.DebugMsg, "Connections loaded using IConnectionDefinitionStore");
        }

        /// <summary>
        /// When turned on, calls to <see cref="SaveConnectionsAsync(CancellationToken)"/> or
        /// <see cref="RequestSave"/> will not immediately execute.
        /// Instead, they will be deferred until <see cref="EndBatchingSaves"/>
        /// is called.
        /// </summary>
        public void BeginBatchingSaves()
        {
            _batchingSaves = true;
        }

        /// <summary>
        /// Requests one save if a save has been requested
        /// since calling <see cref="BeginBatchingSaves"/>.
        /// </summary>
        public void EndBatchingSaves()
        {
            _batchingSaves = false;

            if (_saveAsyncRequested)
                RequestSave();
            else if (_saveRequested)
                RequestSave();
        }

        /// <summary>
        /// All calls to <see cref="SaveConnectionsAsync(CancellationToken)"/> or <see cref="RequestSave"/>
        /// will be deferred until the returned <see cref="DisposableAction"/> is disposed.
        /// Once disposed, this will request one save if one has been requested.
        /// Place this call in a 'using' block to represent a batched saving context.
        /// </summary>
        /// <returns></returns>
        public IDisposable BatchedSavingContext()
        {
            return new DisposableAction(BeginBatchingSaves, EndBatchingSaves);
        }

        public void DisableRemoteSynchronization() => RemoteConnectionsSyncronizer?.Disable();

        public void EnableRemoteSynchronization() => RemoteConnectionsSyncronizer?.Enable();

        /// <summary>
        /// Saves the currently loaded <see cref="ConnectionTreeModel"/> with
        /// no <see cref="SaveFilter"/>.
        /// </summary>
        public Task SaveConnectionsAsync(CancellationToken cancellationToken = default)
        {
            return SaveConnectionsAsync(ConnectionTreeModel, UsingDatabase, new SaveFilter(), ConnectionFileName, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Saves the given <see cref="ConnectionTreeModel"/>.
        /// If <see cref="useDatabase"/> is true, <see cref="connectionFileName"/> is ignored
        /// </summary>
        /// <param name="connectionTreeModel"></param>
        /// <param name="useDatabase"></param>
        /// <param name="saveFilter"></param>
        /// <param name="connectionFileName"></param>
        /// <param name="forceSave">Bypasses safety checks that prevent saving if a connection file isn't loaded.</param>
        /// <param name="propertyNameTrigger">
        /// Optional. The name of the property that triggered
        /// this save.
        /// </param>
        public async Task SaveConnectionsAsync(ConnectionTreeModel connectionTreeModel, bool useDatabase, SaveFilter saveFilter, string connectionFileName, bool forceSave = false, string propertyNameTrigger = "", CancellationToken cancellationToken = default)
        {
            if (connectionTreeModel == null)
                return;

            if (!forceSave && !IsConnectionsFileLoaded)
                return;

            if (_batchingSaves)
            {
                _saveRequested = true;
                return;
            }

            try
            {
                Semaphore? fileSaveSemaphore = null;
                bool ownsFileSaveSemaphore = false;
                if (!useDatabase)
                {
                    Semaphore semaphore = CreateFileSaveSemaphore(connectionFileName);
                    fileSaveSemaphore = semaphore;
                    ownsFileSaveSemaphore = await Task.Run(
                        () => semaphore.WaitOne(TimeSpan.FromSeconds(10)),
                        cancellationToken).ConfigureAwait(false);
                    if (!ownsFileSaveSemaphore)
                        throw new TimeoutException($"Timed out waiting to save connection file: {connectionFileName}");
                }

                try
                {
                    if (!useDatabase && !forceSave && !HasExpectedFileVersion(connectionFileName))
                    {
                        _messageCollector.AddMessage(
                            MessageClass.WarningMsg,
                            $"Connection file changed outside this instance; save was cancelled: {connectionFileName}",
                            true);
                        return;
                    }

                    _messageCollector.AddMessage(MessageClass.InformationMsg, "Saving connections...", onlyLog: true);
                    RemoteConnectionsSyncronizer?.Disable();

                    bool previouslyUsingDatabase = UsingDatabase;

                    await SaveToStoreAsync(useDatabase, connectionFileName, connectionTreeModel, cancellationToken).ConfigureAwait(false);

                    if (UsingDatabase)
                        LastSqlUpdate = DateTime.Now.ToUniversalTime();

                    UsingDatabase = useDatabase;
                    ConnectionFileName = connectionFileName;
                    _lastFileContentHash = !useDatabase ? ComputeFileHash(connectionFileName) : string.Empty;
                    RaiseConnectionsSavedEvent(connectionTreeModel, previouslyUsingDatabase, UsingDatabase, connectionFileName);
                    _messageCollector.AddMessage(MessageClass.InformationMsg, "Successfully saved connections");
                }
                finally
                {
                    if (ownsFileSaveSemaphore)
                        fileSaveSemaphore?.Release();
                    fileSaveSemaphore?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage(FormatText(Language.ConnectionsFileCouldNotSaveAs, connectionFileName), ex, logOnly: false);
            }
            finally
            {
                RemoteConnectionsSyncronizer?.Enable();
            }
        }

        /// <summary>
        /// Save the currently loaded connections asynchronously
        /// </summary>
        /// <param name="propertyNameTrigger">
        /// Optional. The name of the property that triggered
        /// this save.
        /// </param>
        public void RequestSave(string propertyNameTrigger = "")
        {
            if (_batchingSaves)
            {
                _saveAsyncRequested = true;
                return;
            }

            if (!_asyncSaveRequestQueue.Queue(propertyNameTrigger))
                return;

            _ = ProcessAsyncSaveRequestsAsync();
        }

        private async Task ProcessAsyncSaveRequestsAsync()
        {
            while (true)
            {
                await Task.Delay(AsyncSaveCoalesceDelayMilliseconds).ConfigureAwait(false);
                if (_asyncSaveRequestQueue.TryTake(out string propertyNameTrigger))
                {
                    await SaveConnectionsAsync(ConnectionTreeModel, UsingDatabase, new SaveFilter(), ConnectionFileName,
                        propertyNameTrigger: propertyNameTrigger).ConfigureAwait(false);
                }

                if (!_asyncSaveRequestQueue.CompleteSaveAndHasPendingRequest())
                    return;
            }
        }

        private bool HasExpectedFileVersion(string connectionFileName)
        {
            if (string.IsNullOrEmpty(connectionFileName) || string.IsNullOrEmpty(_lastFileContentHash))
                return true;

            return string.Equals(_lastFileContentHash, ComputeFileHash(connectionFileName), StringComparison.Ordinal);
        }

        private static string ComputeFileHash(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
                return string.Empty;

            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fileName)));
        }

        private static Semaphore CreateFileSaveSemaphore(string connectionFileName)
        {
            string semaphoreName = "LoipvRemote_ConnectionFile_" + Convert.ToHexString(
                SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(connectionFileName))));
            return new Semaphore(1, 1, semaphoreName);
        }

        private async Task<ConnectionTreeModel> LoadFromStoreAsync(bool useDatabase, string connectionFileName, CancellationToken cancellationToken)
        {
            ConnectionDefinitionStoreOptions options = _optionsProvider.GetOptions(useDatabase, connectionFileName);
            ConnectionTreeDefinition definition = await _configurationService.LoadAsync(options, cancellationToken).ConfigureAwait(true);
            return ConnectionDefinitionMapper.ToDesktopTree(definition, UnprotectConnectionSecret);
        }

        private async Task SaveToStoreAsync(bool useDatabase, string connectionFileName, ConnectionTreeModel tree, CancellationToken cancellationToken)
        {
            ConnectionDefinitionStoreOptions options = _optionsProvider.GetOptions(useDatabase, connectionFileName);
            ConnectionTreeDefinition definition = ConnectionDefinitionMapper.ToDomainTree(
                tree.RootNodes,
                ProtectConnectionSecret,
                _externalApplicationResolver);
            await _configurationService.SaveAsync(options, definition, cancellationToken).ConfigureAwait(false);
        }

        private string ProtectConnectionSecret(string connectionId, string propertyName, string plaintext) =>
            _secretStore.Protect(plaintext, ConnectionSecretPurposes.ForConnectionOption(connectionId, propertyName));

        private string UnprotectConnectionSecret(string connectionId, string propertyName, string protectedValue) =>
            _secretStore.Unprotect(protectedValue, ConnectionSecretPurposes.ForConnectionOption(connectionId, propertyName));

        private static object? NormalizeOptions(ConnectionNodeOptions? options) => options is null
            ? null
            : new
            {
                Values = options.Values.OrderBy(value => value.Key, StringComparer.Ordinal),
                InheritedProperties = options.InheritedProperties.OrderBy(name => name, StringComparer.Ordinal)
            };

        public string GetStartupConnectionFileName()
        {
            /*
            if (Properties.OptionsBackupPage.Default.LoadConsFromCustomLocation == true && Properties.OptionsBackupPage.Default.BackupLocation != "")
            {
                return Properties.OptionsBackupPage.Default.BackupLocation;
            } else {
                return GetDefaultStartupConnectionFileName();
            }
            */
            if (Properties.OptionsConnectionsPage.Default.ConnectionFilePath != "")
            {
                return Properties.OptionsConnectionsPage.Default.ConnectionFilePath;
            }
            else
            {
                return GetDefaultStartupConnectionFileName();
            }
        }

        public static string GetDefaultStartupConnectionFileName()
        {
            return ApplicationEdition.IsPortable ? GetDefaultStartupConnectionFileNamePortableEdition() : GetDefaultStartupConnectionFileNameNormalEdition();
        }

        private static void UpdateCustomConsPathSetting(string filename)
        {
            if (filename == GetDefaultStartupConnectionFileName())
            {
                Properties.OptionsBackupPage.Default.LoadConsFromCustomLocation = false;
            }
            else
            {
                Properties.OptionsBackupPage.Default.LoadConsFromCustomLocation = true;
                Properties.OptionsBackupPage.Default.BackupLocation = filename;
            }
        }

        private static string GetDefaultStartupConnectionFileNameNormalEdition()
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Application.ProductName ?? string.Empty,
                ConnectionsFileInfo.DefaultConnectionsFile);
            return File.Exists(appDataPath) ? appDataPath : GetDefaultStartupConnectionFileNamePortableEdition();
        }

        private static string GetDefaultStartupConnectionFileNamePortableEdition()
        {
            return Path.Combine(ConnectionsFileInfo.DefaultConnectionsPath, ConnectionsFileInfo.DefaultConnectionsFile);
        }

        #region Events

        public event EventHandler<ConnectionsLoadedEventArgs>? ConnectionsLoaded;
        public event EventHandler<ConnectionsSavedEvent>? ConnectionsSaved;

        private void RaiseConnectionsLoadedEvent(OptionalValue<ConnectionTreeModel> previousTreeModel, ConnectionTreeModel newTreeModel, bool previousSourceWasDatabase, bool newSourceIsDatabase, string newSourcePath)
        {
            ConnectionsLoaded?.Invoke(this, new ConnectionsLoadedEventArgs(previousTreeModel, newTreeModel, previousSourceWasDatabase, newSourceIsDatabase, newSourcePath));
        }

        private void RaiseConnectionsSavedEvent(ConnectionTreeModel modelThatWasSaved, bool previouslyUsingDatabase, bool usingDatabase, string connectionFileName)
        {
            ConnectionsSaved?.Invoke(this, new ConnectionsSavedEvent(modelThatWasSaved, previouslyUsingDatabase, usingDatabase, connectionFileName));
        }

        #endregion
    }
}
