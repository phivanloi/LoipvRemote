using System.ComponentModel;
using System.Reflection;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;

namespace LoipvRemote.UI.Adapters;

/// <summary>Explicit, secret-free boundary between desktop tree nodes and Domain definitions.</summary>
public static class ConnectionDefinitionMapper
{
    private const string ProtectedSecretOptionPrefix = "$dpapi-secret:";
    private const string FolderExpandedOptionName = "$desktop-folder:is-expanded";
    private static readonly HashSet<string> CorePropertyNames =
    [
        nameof(ConnectionInfo.Name),
        nameof(ConnectionInfo.Hostname),
        nameof(ConnectionInfo.Port),
        nameof(ConnectionInfo.Protocol),
        nameof(ConnectionInfo.ExtApp),
        nameof(ConnectionInfo.ExternalCredentialProvider),
        nameof(ConnectionInfo.UserViaAPI),
        nameof(ConnectionInfo.RDGatewayExternalCredentialProvider),
        nameof(ConnectionInfo.RDGatewayUserViaAPI)
    ];

    private static readonly HashSet<string> SecretPropertyNames =
    [
        nameof(ConnectionInfo.Password),
        nameof(ConnectionInfo.RDGatewayPassword),
        nameof(ConnectionInfo.RDGatewayAccessToken),
        nameof(ConnectionInfo.VNCProxyPassword)
    ];

    public static ConnectionTreeDefinition ToDomainTree(
        IEnumerable<ContainerInfo> rootNodes,
        Func<string, string, string, string>? protectSecret = null,
        Func<ConnectionInfo, ExternalApplicationDefinition?>? externalApplicationResolver = null)
    {
        ArgumentNullException.ThrowIfNull(rootNodes);

        var folders = new List<ConnectionFolderDefinition>();
        var connections = new List<ConnectionDefinition>();
        int rootSortOrder = 0;
        foreach (ContainerInfo rootNode in rootNodes)
        {
            if (rootNode is RootNodeInfo connectionRoot)
            {
                // Runtime roots, such as "PuTTY Saved Sessions", are projected into
                // the UI alongside the persisted connection root. They can contain
                // host-less session placeholders and must never reach Domain storage.
                if (connectionRoot.Type != RootNodeType.Connection)
                    continue;

                Guid rootId = ParseId(rootNode.ConstantID, nameof(rootNode));
                folders.Add(new ConnectionFolderDefinition(
                    rootId,
                    rootNode.Name,
                    ParentFolderId: null,
                    SortOrder: rootSortOrder++,
                    Options: ToFolderOptions(rootNode, protectSecret),
                    IsRoot: true));
                MapChildren(rootNode.Children, rootId, folders, connections, protectSecret, externalApplicationResolver);
                continue;
            }

            // Tests and imported trees may have a synthetic ContainerInfo root.
            // It is an implementation detail and was never persisted as a tree node.
            MapChildren(rootNode.Children, parentFolderId: null, folders, connections, protectSecret, externalApplicationResolver);
        }

        var tree = new ConnectionTreeDefinition(folders, connections);
        tree.Validate();
        return tree;
    }

    public static ConnectionDefinition ToDomain(
        ConnectionInfo connection,
        Func<string, string, string, string>? protectSecret = null,
        Func<ConnectionInfo, ExternalApplicationDefinition?>? externalApplicationResolver = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!Guid.TryParse(connection.ConstantID, out Guid id))
            throw new ArgumentException("Connection ID must be a GUID.", nameof(connection));

        ConnectionNodeOptions? options = ToOptions(connection, protectSecret);
        return ReadLocalValues(connection, () =>
        {
            ProtocolKind protocol = connection.Protocol;
            ExternalApplicationDefinition? externalApplication = protocol == ProtocolKind.ExternalApplication
                ? externalApplicationResolver?.Invoke(connection)
                    ?? throw new InvalidOperationException("External application connections require an explicit definition resolver.")
                : null;

            return new ConnectionDefinition(
                id,
                connection.Name,
                connection.Hostname,
                connection.Port,
                protocol,
                ToCredentialReference(connection.ExternalCredentialProvider, connection.UserViaAPI),
                externalApplication,
                Options: options,
                GatewayCredential: ToCredentialReference(
                    connection.RDGatewayExternalCredentialProvider,
                    connection.RDGatewayUserViaAPI));
        });
    }

    public static ConnectionTreeModel ToDesktopTree(
        ConnectionTreeDefinition tree,
        Func<string, string, string, string>? unprotectSecret = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        tree.Validate();

        var model = new ConnectionTreeModel();
        var folders = tree.Folders.ToDictionary(folder => folder.Id, CreateDesktopFolder);
        var configuredNodes = new List<(ConnectionInfo Node, ConnectionNodeOptions? Options)>();

        foreach (ConnectionFolderDefinition definition in tree.Folders)
        {
            ContainerInfo folder = folders[definition.Id];
            ApplyFolderOptions(folder, definition.Options, unprotectSecret);
            folder.Name = definition.Name;
            configuredNodes.Add((folder, definition.Options));
        }

        foreach (ConnectionFolderDefinition definition in tree.Folders.OrderBy(folder => folder.SortOrder))
        {
            ContainerInfo folder = folders[definition.Id];
            if (definition.ParentFolderId is Guid parentFolderId)
                folders[parentFolderId].AddChild(folder);
            else
                model.AddRootNode(folder);
        }

        RootNodeInfo? fallbackRoot = null;
        foreach (ConnectionDefinition definition in tree.Connections.OrderBy(connection => connection.SortOrder))
        {
            var connection = new ConnectionInfo(definition.Id.ToString());
            ApplyOptions(connection, definition.Options, unprotectSecret);
            connection.Name = definition.Name;
            connection.Hostname = definition.Host;
            connection.Port = definition.Port;
            connection.Protocol = ToDesktopProtocol(definition.Protocol, definition.Options);
            ApplyCredentialReference(connection, definition.Credential, isGateway: false);
            ApplyCredentialReference(connection, definition.GatewayCredential ?? CredentialReference.None, isGateway: true);
            if (definition.ExternalApplication is not null)
                connection.ExtApp = definition.ExternalApplication.DisplayName;

            if (definition.ParentFolderId is Guid parentFolderId)
            {
                folders[parentFolderId].AddChild(connection);
            }
            else
            {
                fallbackRoot ??= new RootNodeInfo(RootNodeType.Connection);
                if (!model.RootNodes.Contains(fallbackRoot))
                    model.AddRootNode(fallbackRoot);
                fallbackRoot.AddChild(connection);
            }

            configuredNodes.Add((connection, definition.Options));
        }

        foreach ((ConnectionInfo node, ConnectionNodeOptions? options) in configuredNodes)
            ApplyInheritance(node, options);

        return model;
    }

    private static ContainerInfo CreateDesktopFolder(ConnectionFolderDefinition definition) =>
        definition.IsRoot
            ? new RootNodeInfo(RootNodeType.Connection, definition.Id.ToString())
            : new ContainerInfo(definition.Id.ToString());

    private static ProtocolKind ToDesktopProtocol(ProtocolKind protocol, ConnectionNodeOptions? options)
    {
        if (protocol != ProtocolKind.Browser)
            return protocol;

        if (options?.Values.TryGetValue("Scheme", out string? scheme) != true)
            throw new NotSupportedException(
                "Browser connections must specify a Domain option named 'Scheme' with value 'http' or 'https'.");

        return scheme.Trim().ToLowerInvariant() switch
        {
            "http" => ProtocolKind.Http,
            "https" => ProtocolKind.Https,
            _ => throw new NotSupportedException(
                "Browser connections must specify a Domain option named 'Scheme' with value 'http' or 'https'.")
        };
    }

    private static CredentialReference ToCredentialReference(
        ExternalCredentialProvider provider,
        string identifier) =>
        provider == ExternalCredentialProvider.None
            ? CredentialReference.None
            : new CredentialReference(provider.ToString(), identifier);

    private static void ApplyCredentialReference(
        ConnectionInfo connection,
        CredentialReference credential,
        bool isGateway)
    {
        credential.Validate();
        ExternalCredentialProvider provider;
        if (string.Equals(credential.Provider, CredentialReference.None.Provider, StringComparison.OrdinalIgnoreCase))
        {
            provider = ExternalCredentialProvider.None;
        }
        else if (!Enum.TryParse(credential.Provider, ignoreCase: false, out provider))
        {
            throw new NotSupportedException($"Credential provider '{credential.Provider}' is not supported by the desktop adapter.");
        }

        if (isGateway)
        {
            connection.RDGatewayExternalCredentialProvider = provider;
            connection.RDGatewayUserViaAPI = credential.Identifier;
            return;
        }

        connection.ExternalCredentialProvider = provider;
        connection.UserViaAPI = credential.Identifier;
    }

    private static ConnectionNodeOptions? ToOptions(
        ConnectionInfo connection,
        Func<string, string, string, string>? protectSecret)
    {
        string[] inheritedProperties = connection.Inheritance
            .GetEnabledInheritanceProperties()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        ReadLocalValues(connection, () =>
        {
            foreach (PropertyInfo property in connection.GetSerializableProperties())
            {
                if (CorePropertyNames.Contains(property.Name))
                    continue;
                if (!property.CanRead || !property.CanWrite)
                    continue;

                if (SecretPropertyNames.Contains(property.Name))
                {
                    if (protectSecret is null)
                        continue;

                    string plaintext = property.GetValue(connection) as string ?? string.Empty;
                    if (!string.IsNullOrEmpty(plaintext))
                    {
                        values.Add(
                            ProtectedSecretOptionPrefix + property.Name,
                            protectSecret(connection.ConstantID, property.Name, plaintext));
                    }

                    continue;
                }

                TypeConverter converter = TypeDescriptor.GetConverter(property.PropertyType);
                if (!converter.CanConvertTo(typeof(string)))
                    throw new NotSupportedException($"Connection option '{property.Name}' cannot be represented as a Domain value.");

                object? value = property.GetValue(connection);
                string? serialized = converter.ConvertToInvariantString(value);
                if (serialized is null)
                    throw new NotSupportedException($"Connection option '{property.Name}' cannot be represented as a Domain value.");

                values.Add(property.Name, serialized);
            }

            return true;
        });

        return values.Count == 0 && inheritedProperties.Length == 0
            ? null
            : new ConnectionNodeOptions(values, inheritedProperties);
    }

    private static ConnectionNodeOptions ToFolderOptions(
        ContainerInfo folder,
        Func<string, string, string, string>? protectSecret)
    {
        ConnectionNodeOptions? options = ToOptions(folder, protectSecret);
        var values = options is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(options.Values, StringComparer.Ordinal);
        values[FolderExpandedOptionName] = folder.IsExpanded.ToString();

        return new ConnectionNodeOptions(
            values,
            options?.InheritedProperties ?? Array.Empty<string>());
    }

    private static T ReadLocalValues<T>(ConnectionInfo connection, Func<T> read)
    {
        ConnectionInfoInheritance inheritance = connection.Inheritance.Clone(connection);
        connection.Inheritance.TurnOffInheritanceCompletely();
        try
        {
            return read();
        }
        finally
        {
            connection.Inheritance = inheritance;
        }
    }

    private static void ApplyOptions(
        ConnectionInfo connection,
        ConnectionNodeOptions? options,
        Func<string, string, string, string>? unprotectSecret)
    {
        connection.Inheritance.TurnOffInheritanceCompletely();
        if (options is null)
            return;

        options.Validate();
        foreach ((string name, string serializedValue) in options.Values)
        {
            if (name == FolderExpandedOptionName)
                continue;

            if (name.StartsWith(ProtectedSecretOptionPrefix, StringComparison.Ordinal))
            {
                if (unprotectSecret is null)
                    throw new InvalidOperationException("A DPAPI secret resolver is required to restore protected connection options.");

                string propertyName = name[ProtectedSecretOptionPrefix.Length..];
                if (!SecretPropertyNames.Contains(propertyName))
                    throw new ArgumentException($"'{name}' is not a supported protected connection option.", nameof(options));

                PropertyInfo? secretProperty = typeof(ConnectionInfo).GetProperty(propertyName);
                if (secretProperty?.PropertyType != typeof(string) || !secretProperty.CanWrite)
                    throw new NotSupportedException($"Connection secret '{propertyName}' is not supported by the desktop adapter.");

                secretProperty.SetValue(connection, unprotectSecret(connection.ConstantID, propertyName, serializedValue));
                continue;
            }

            if (CorePropertyNames.Contains(name) || SecretPropertyNames.Contains(name))
                throw new ArgumentException($"'{name}' is not a valid non-secret Domain option.", nameof(options));

            PropertyInfo? property = typeof(ConnectionInfo).GetProperty(name);
            if (property is null || !property.CanWrite)
                throw new NotSupportedException($"Connection option '{name}' is not supported by the desktop adapter.");

            TypeConverter converter = TypeDescriptor.GetConverter(property.PropertyType);
            if (!converter.CanConvertFrom(typeof(string)))
                throw new NotSupportedException($"Connection option '{name}' cannot be restored from a Domain value.");

            object? value = converter.ConvertFromInvariantString(serializedValue);
            property.SetValue(connection, value);
        }
    }

    private static void ApplyFolderOptions(
        ContainerInfo folder,
        ConnectionNodeOptions? options,
        Func<string, string, string, string>? unprotectSecret)
    {
        ApplyOptions(folder, options, unprotectSecret);
        if (options?.Values.TryGetValue(FolderExpandedOptionName, out string? isExpanded) == true &&
            bool.TryParse(isExpanded, out bool expanded))
        {
            folder.IsExpanded = expanded;
        }
    }

    private static void ApplyInheritance(ConnectionInfo connection, ConnectionNodeOptions? options)
    {
        if (options is null)
            return;

        foreach (string propertyName in options.InheritedProperties)
        {
            PropertyInfo? property = typeof(ConnectionInfoInheritance).GetProperty(propertyName);
            if (property?.PropertyType != typeof(bool) || !property.CanWrite)
                throw new NotSupportedException($"Inheritance property '{propertyName}' is not supported by the desktop adapter.");

            property.SetValue(connection.Inheritance, true);
        }
    }

    private static void MapChildren(
        IReadOnlyList<ConnectionInfo> children,
        Guid? parentFolderId,
        ICollection<ConnectionFolderDefinition> folders,
        ICollection<ConnectionDefinition> connections,
        Func<string, string, string, string>? protectSecret,
        Func<ConnectionInfo, ExternalApplicationDefinition?>? externalApplicationResolver)
    {
        for (int index = 0; index < children.Count; index++)
        {
            ConnectionInfo child = children[index];
            if (child is ContainerInfo folder)
            {
                Guid folderId = ParseId(folder.ConstantID, nameof(folder));
                folders.Add(new ConnectionFolderDefinition(
                    folderId,
                    folder.Name,
                    parentFolderId,
                    index,
                    ToFolderOptions(folder, protectSecret)));
                MapChildren(folder.Children, folderId, folders, connections, protectSecret, externalApplicationResolver);
                continue;
            }

            connections.Add(ToDomain(child, protectSecret, externalApplicationResolver) with { ParentFolderId = parentFolderId, SortOrder = index });
        }
    }

    private static Guid ParseId(string value, string parameterName)
    {
        if (Guid.TryParse(value, out Guid id))
            return id;

        throw new ArgumentException("Connection ID must be a GUID.", parameterName);
    }
}
