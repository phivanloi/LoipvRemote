using LoipvRemote.Domain.Connections;
using LoipvRemote.Connection;
using LoipvRemote.Config.Serializers.ConnectionSerializers.Xml;
using LoipvRemote.Properties;
using LoipvRemote.Tree;
using LoipvRemote.UI.Adapters;
using LoipvRemote.UseCases.Configuration;
using LoipvRemote.UseCases.Credentials;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace LoipvRemote.App;

/// <summary>Configures a Domain persistence store from desktop settings and maps it at the UI boundary.</summary>
public sealed class ConnectionStoreRuntime(
    ConnectionDefinitionPersistenceRuntime definitionRuntime,
    IConnectionStoreOptionsProvider optionsProvider,
    IStringSecretStore secretStore,
    Func<ConnectionInfo, ExternalApplicationDefinition?>? externalApplicationResolver = null)
{
    private readonly ConnectionDefinitionPersistenceRuntime _definitionRuntime = definitionRuntime ?? throw new ArgumentNullException(nameof(definitionRuntime));
    private readonly IConnectionStoreOptionsProvider _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
    private readonly IStringSecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    private readonly Func<ConnectionInfo, ExternalApplicationDefinition?>? _externalApplicationResolver = externalApplicationResolver;
    private const string ConnectionSecretPurposePrefix = "connection-secret";
    // Historical confCons.xml files used this documented format password when no
    // user-defined root password was configured. It is used only to import the
    // old format and never written to the new store.
    private const string LegacyDefaultRootPassword = "mR3m";

    public ConnectionTreeModel Load(bool useDatabase, string connectionFileName) =>
        LoadAsync(useDatabase, connectionFileName).GetAwaiter().GetResult();

    /// <summary>
    /// Loads a connection tree without capturing the caller's synchronization context.
    /// The legacy WinForms callers still expose a synchronous API, so persistence must
    /// run away from the UI thread to avoid deadlocking when an asynchronous store resumes.
    /// </summary>
    public async Task<ConnectionTreeModel> LoadAsync(
        bool useDatabase,
        string connectionFileName,
        CancellationToken cancellationToken = default)
    {
        ConnectionDefinitionStoreOptions options = _optionsProvider.GetOptions(useDatabase, connectionFileName);
        ConnectionTreeDefinition definition = await Task.Run(
                () => LoadDefinitionAsync(options, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
        return ConnectionDefinitionMapper.ToDesktopTree(definition, UnprotectConnectionSecret);
    }

    public void Save(bool useDatabase, string connectionFileName, ConnectionTreeModel tree) =>
        SaveAsync(useDatabase, connectionFileName, tree).GetAwaiter().GetResult();

    public Task SaveAsync(
        bool useDatabase,
        string connectionFileName,
        ConnectionTreeModel tree,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ConnectionTreeDefinition definition = ConnectionDefinitionMapper.ToDomainTree(
            tree.RootNodes,
            ProtectConnectionSecret,
            _externalApplicationResolver);
        ConnectionDefinitionStoreOptions options = _optionsProvider.GetOptions(useDatabase, connectionFileName);
        return Task.Run(
            () => _definitionRuntime.SaveAsync(options, definition, cancellationToken),
            cancellationToken);
    }

    /// <summary>Returns a stable, secret-free revision for polling a configured database store.</summary>
    public string GetDatabaseRevision()
    {
        ConnectionDefinitionStoreOptions options = _optionsProvider.GetOptions(useDatabase: true, connectionFileName: string.Empty);
        ConnectionTreeDefinition definition = Task.Run(
                () => _definitionRuntime.LoadAsync(options),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

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

    private static object? NormalizeOptions(ConnectionNodeOptions? options) => options is null
        ? null
        : new
        {
            Values = options.Values.OrderBy(value => value.Key, StringComparer.Ordinal),
            InheritedProperties = options.InheritedProperties.OrderBy(name => name, StringComparer.Ordinal)
        };

    private async Task<ConnectionTreeDefinition> LoadDefinitionAsync(
        ConnectionDefinitionStoreOptions options,
        CancellationToken cancellationToken)
    {
        if (options.Kind == ConnectionDefinitionStoreKind.Xml && IsLegacyConnectionFile(options.Location))
            return await MigrateLegacyXmlAsync(options, cancellationToken).ConfigureAwait(false);

        return await _definitionRuntime.LoadAsync(options, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ConnectionTreeDefinition> MigrateLegacyXmlAsync(
        ConnectionDefinitionStoreOptions options,
        CancellationToken cancellationToken)
    {
        string legacyXml = await File.ReadAllTextAsync(options.Location, cancellationToken).ConfigureAwait(false);
        // The legacy root marker is encrypted with the old format default. Supplying
        // it explicitly avoids the obsolete interactive password callback on startup.
        var deserializer = new XmlConnectionsDeserializer
        {
            InitialAuthenticationPassword = LegacyDefaultRootPassword
        };
        ConnectionTreeModel legacyTree = deserializer.Deserialize(legacyXml, import: true)
            ?? throw new InvalidDataException("The legacy connection file could not be authenticated.");

        ConnectionTreeDefinition definition = ConnectionDefinitionMapper.ToDomainTree(
            legacyTree.RootNodes,
            ProtectConnectionSecret,
            _externalApplicationResolver);
        string backupPath = $"{options.Location}.legacy-{DateTime.UtcNow:yyyyMMddHHmmss}.backup";
        File.Copy(options.Location, backupPath, overwrite: false);
        await _definitionRuntime.SaveAsync(options, definition, cancellationToken).ConfigureAwait(false);
        return definition;
    }

    private static bool IsLegacyConnectionFile(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        using FileStream source = File.OpenRead(filePath);
        XDocument document = XDocument.Load(source, LoadOptions.None);
        return string.Equals(document.Root?.Name.LocalName, "Connections", StringComparison.Ordinal);
    }

    private string ProtectConnectionSecret(string connectionId, string propertyName, string plaintext) =>
        _secretStore.Protect(plaintext, CreateConnectionSecretPurpose(connectionId, propertyName));

    private string UnprotectConnectionSecret(string connectionId, string propertyName, string protectedValue) =>
        _secretStore.Unprotect(protectedValue, CreateConnectionSecretPurpose(connectionId, propertyName));

    private static string CreateConnectionSecretPurpose(string connectionId, string propertyName) =>
        $"{ConnectionSecretPurposePrefix}:{connectionId}:{propertyName}";

}
