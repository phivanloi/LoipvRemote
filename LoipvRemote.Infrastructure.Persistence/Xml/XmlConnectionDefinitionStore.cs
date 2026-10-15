using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using LoipvRemote.Application.Configuration;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Validation;

namespace LoipvRemote.Infrastructure.Persistence.Xml;

/// <summary>
/// XML persistence for Domain connection definitions. Normal saves exclude
/// secrets; portable exports explicitly opt into plaintext credentials.
/// </summary>
public sealed class XmlConnectionDefinitionStore : IConnectionDefinitionStore
{
    private const int BackupRetentionCount = 10;
    private const string RootName = "connections";
    private const string FolderName = "folder";
    private const string ConnectionName = "connection";
    private const string ParentFolderIdAttribute = "parentFolderId";
    private const string SortOrderAttribute = "sortOrder";
    private const string OptionsAttribute = "options";
    private const string IsRootAttribute = "isRoot";
    private const string GatewayCredentialProviderAttribute = "gatewayCredentialProvider";
    private const string GatewayCredentialIdentifierAttribute = "gatewayCredentialIdentifier";
    private const string ExportUserNameAttribute = "exportUsername";
    private const string ExportPasswordAttribute = "exportPassword";
    private const string ExportGatewayPasswordAttribute = "exportGatewayPassword";
    private readonly string _filePath;

    public XmlConnectionDefinitionStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<ConnectionTreeDefinition> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        ConnectionExportPackage package = await LoadPortableAsync(cancellationToken).ConfigureAwait(false);
        return package.Tree;
    }

    /// <summary>Loads an optional plaintext credential payload from an export file.</summary>
    public async Task<ConnectionExportPackage> LoadPortableAsync(
        CancellationToken cancellationToken = default)
    {
        await using FileStream source = new(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var document = await XDocument.LoadAsync(source, LoadOptions.None, cancellationToken).ConfigureAwait(false);
        if (document.Root?.Name != RootName)
            throw new InvalidDataException($"Expected '{RootName}' as the XML root element.");

        return ParseExportPackage(document.Root);
    }

    public async Task SaveAsync(
        ConnectionTreeDefinition tree,
        CancellationToken cancellationToken = default)
        => await SaveCoreAsync(tree, credentials: null, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Saves a portable XML file with plaintext credentials so another machine
    /// can import and immediately protect them with its own DPAPI context.
    /// </summary>
    public async Task SavePortableAsync(
        ConnectionTreeDefinition tree,
        IReadOnlyDictionary<Guid, PortableConnectionCredential> credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(credentials);
        if (credentials.Keys.Any(id => !tree.Connections.Any(connection => connection.Id == id)))
            throw new ArgumentException("Portable credentials must reference a connection in the exported tree.", nameof(credentials));

        await SaveCoreAsync(tree, credentials, cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveCoreAsync(
        ConnectionTreeDefinition tree,
        IReadOnlyDictionary<Guid, PortableConnectionCredential>? credentials,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tree);
        tree.Validate();

        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await BackupExistingFileAsync(cancellationToken).ConfigureAwait(false);
            await using (FileStream destination = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                var document = new XDocument(
                    new XElement(
                        RootName,
                        tree.Folders.OrderBy(folder => folder.SortOrder).Select(SerializeFolder),
                        tree.Connections.OrderBy(connection => connection.SortOrder).Select(connection =>
                            SerializeConnection(
                                connection,
                                credentials is not null && credentials.TryGetValue(connection.Id, out PortableConnectionCredential? credential)
                                    ? credential
                                    : null))));
                await document.SaveAsync(destination, SaveOptions.None, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private async Task BackupExistingFileAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return;

        string directory = Path.GetDirectoryName(_filePath) ?? throw new InvalidOperationException("The XML connection file has no directory.");
        string backupDirectory = Path.Combine(directory, "backups");
        Directory.CreateDirectory(backupDirectory);
        string name = Path.GetFileNameWithoutExtension(_filePath);
        string extension = Path.GetExtension(_filePath);
        string backupPath = Path.Combine(
            backupDirectory,
            $"{name}.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}{extension}");

        await using (FileStream source = new(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (FileStream destination = new(
            backupPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (FileInfo expired in new DirectoryInfo(backupDirectory)
                     .EnumerateFiles($"{name}.*{extension}")
                     .OrderByDescending(file => file.CreationTimeUtc)
                     .Skip(BackupRetentionCount))
        {
            expired.Delete();
        }
    }

    private static XElement SerializeFolder(ConnectionFolderDefinition folder) =>
        new(
            FolderName,
            new XAttribute("id", folder.Id),
            new XAttribute("name", folder.Name),
            new XAttribute(SortOrderAttribute, folder.SortOrder),
            folder.IsRoot ? new XAttribute(IsRootAttribute, true) : null,
            SerializeOptions(folder.Options),
            folder.ParentFolderId is { } parentFolderId
                ? new XAttribute(ParentFolderIdAttribute, parentFolderId)
                : null);

    private static XElement SerializeConnection(
        ConnectionDefinition definition,
        PortableConnectionCredential? portableCredential = null)
    {
        ConnectionDefinitionValidator.Validate(definition);

        var connection = new XElement(
            ConnectionName,
            new XAttribute("id", definition.Id),
            new XAttribute("name", definition.Name),
            new XAttribute("host", definition.Host),
            new XAttribute("port", definition.Port),
            new XAttribute("protocol", definition.Protocol),
            new XAttribute("credentialProvider", definition.Credential.Provider),
            new XAttribute("credentialIdentifier", definition.Credential.Identifier),
            new XAttribute(SortOrderAttribute, definition.SortOrder),
            SerializeOptions(definition.Options),
            definition.GatewayCredential is { } gatewayCredential
                ? new XAttribute(GatewayCredentialProviderAttribute, gatewayCredential.Provider)
                : null,
            definition.GatewayCredential is { } gatewayIdentifier
                ? new XAttribute(GatewayCredentialIdentifierAttribute, gatewayIdentifier.Identifier)
                : null,
            portableCredential is { UserName.Length: > 0 }
                ? new XAttribute(ExportUserNameAttribute, portableCredential.UserName)
                : null,
            portableCredential is { Password.Length: > 0 }
                ? new XAttribute(ExportPasswordAttribute, portableCredential.Password)
                : null,
            portableCredential is { GatewayPassword.Length: > 0 }
                ? new XAttribute(ExportGatewayPasswordAttribute, portableCredential.GatewayPassword)
                : null,
            definition.ParentFolderId is { } parentFolderId
                ? new XAttribute(ParentFolderIdAttribute, parentFolderId)
                : null);

        return connection;
    }

    private static ConnectionFolderDefinition ParseFolder(XElement element)
    {
        if (!Guid.TryParse((string?)element.Attribute("id"), out var id))
            throw new InvalidDataException("Connection folder XML has an invalid id.");

        return new ConnectionFolderDefinition(
            id,
            (string?)element.Attribute("name") ?? string.Empty,
            ParseOptionalGuid(element, ParentFolderIdAttribute),
            ParseSortOrder(element),
            ParseOptions(element),
            ParseOptionalBoolean(element, IsRootAttribute));
    }

    private static ConnectionDefinition ParseConnection(XElement element)
    {
        if (!Guid.TryParse((string?)element.Attribute("id"), out var id))
            throw new InvalidDataException("Connection XML has an invalid id.");
        if (!int.TryParse((string?)element.Attribute("port"), NumberStyles.None, CultureInfo.InvariantCulture, out var port))
            throw new InvalidDataException("Connection XML has an invalid port.");
        if (!Enum.TryParse<ProtocolKind>((string?)element.Attribute("protocol"), out var protocol))
            throw new InvalidDataException("Connection XML has an invalid protocol.");

        var provider = (string?)element.Attribute("credentialProvider") ?? CredentialReference.None.Provider;
        var identifier = (string?)element.Attribute("credentialIdentifier") ?? string.Empty;
        var definition = new ConnectionDefinition(
            id,
            (string?)element.Attribute("name") ?? string.Empty,
            (string?)element.Attribute("host") ?? string.Empty,
            port,
            protocol,
            new CredentialReference(provider, identifier),
            ParentFolderId: ParseOptionalGuid(element, ParentFolderIdAttribute),
            SortOrder: ParseSortOrder(element),
            Options: ParseOptions(element),
            GatewayCredential: ParseGatewayCredential(element));

        ConnectionDefinitionValidator.Validate(definition);
        return definition;
    }

    private static ConnectionExportPackage ParseExportPackage(XElement root)
    {
        ParsedConnection[] parsedConnections = root.Elements(ConnectionName)
            .Select(element => new ParsedConnection(ParseConnection(element), ParsePortableCredential(element)))
            .ToArray();
        var tree = new ConnectionTreeDefinition(
            root.Elements(FolderName).Select(ParseFolder).ToArray(),
            parsedConnections.Select(item => item.Definition).ToArray());
        tree.Validate();

        return new ConnectionExportPackage(
            tree,
            parsedConnections
                .Where(item => item.Credential is not null)
                .ToDictionary(item => item.Definition.Id, item => item.Credential!));
    }

    private static PortableConnectionCredential? ParsePortableCredential(XElement element)
    {
        string? userName = (string?)element.Attribute(ExportUserNameAttribute);
        string? password = (string?)element.Attribute(ExportPasswordAttribute);
        string? gatewayPassword = (string?)element.Attribute(ExportGatewayPasswordAttribute);
        return userName is null && password is null && gatewayPassword is null
            ? null
            : new PortableConnectionCredential(userName ?? string.Empty, password ?? string.Empty, gatewayPassword ?? string.Empty);
    }

    private static Guid? ParseOptionalGuid(XElement element, string attribute)
    {
        string? value = (string?)element.Attribute(attribute);
        if (value is null)
            return null;
        if (Guid.TryParse(value, out Guid id))
            return id;

        throw new InvalidDataException($"Connection XML has an invalid '{attribute}'.");
    }

    private static int ParseSortOrder(XElement element)
    {
        string? value = (string?)element.Attribute(SortOrderAttribute);
        if (value is null)
            return 0;
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int sortOrder))
            return sortOrder;

        throw new InvalidDataException("Connection XML has an invalid sort order.");
    }

    private static bool ParseBooleanAttribute(XElement element, string attribute)
    {
        if (bool.TryParse((string?)element.Attribute(attribute), out bool value))
            return value;

        throw new InvalidDataException($"Connection XML has an invalid '{attribute}' value.");
    }

    private static bool ParseOptionalBoolean(XElement element, string attribute)
    {
        XAttribute? value = element.Attribute(attribute);
        return value is not null && ParseBooleanAttribute(element, attribute);
    }

    private static XAttribute? SerializeOptions(ConnectionNodeOptions? options) =>
        options is null
            ? null
            : new XAttribute(OptionsAttribute,
                Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(options)));

    private static ConnectionNodeOptions? ParseOptions(XElement element)
    {
        string? encoded = (string?)element.Attribute(OptionsAttribute);
        if (encoded is null)
            return null;

        try
        {
            ConnectionNodeOptions? options = JsonSerializer.Deserialize<ConnectionNodeOptions>(Convert.FromBase64String(encoded));
            if (options is null)
                throw new InvalidDataException("Connection XML has invalid options.");
            options.Validate();
            return options;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            throw new InvalidDataException("Connection XML has invalid options.", exception);
        }
    }

    private static CredentialReference? ParseGatewayCredential(XElement element)
    {
        string? provider = (string?)element.Attribute(GatewayCredentialProviderAttribute);
        string? identifier = (string?)element.Attribute(GatewayCredentialIdentifierAttribute);
        if (provider is null && identifier is null)
            return null;
        if (provider is null || identifier is null)
            throw new InvalidDataException("Connection XML has an incomplete gateway credential reference.");

        var credential = new CredentialReference(provider, identifier);
        try
        {
            credential.Validate();
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Connection XML has an invalid gateway credential reference.", exception);
        }

        return credential;
    }

    private sealed record ParsedConnection(
        ConnectionDefinition Definition,
        PortableConnectionCredential? Credential);
}
