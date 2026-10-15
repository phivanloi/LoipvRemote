namespace LoipvRemote.Domain.Connections;

/// <summary>Resolves folder and connection options into a session-ready definition.</summary>
public static class ConnectionOptionInheritanceResolver
{
    public static ConnectionDefinition Resolve(ConnectionTreeDefinition tree, Guid connectionId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        tree.Validate();
        ConnectionDefinition connection = tree.Connections.SingleOrDefault(candidate => candidate.Id == connectionId)
            ?? throw new ArgumentException("The connection no longer exists in the current tree.", nameof(connectionId));

        var folders = tree.Folders.ToDictionary(folder => folder.Id);
        var chain = new Stack<ConnectionFolderDefinition>();
        Guid? parentId = connection.ParentFolderId;
        while (parentId is { } id)
        {
            ConnectionFolderDefinition folder = folders[id];
            chain.Push(folder);
            parentId = folder.ParentFolderId;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        while (chain.TryPop(out ConnectionFolderDefinition? folder))
            Apply(values, folder.Options);
        Apply(values, connection.Options);

        return connection with
        {
            Options = values.Count == 0 ? null : new ConnectionNodeOptions(values, [])
        };
    }

    private static void Apply(Dictionary<string, string> effectiveValues, ConnectionNodeOptions? options)
    {
        if (options is null)
            return;

        var inherited = new HashSet<string>(options.InheritedProperties, StringComparer.Ordinal);
        foreach ((string key, string value) in options.Values)
        {
            if (!inherited.Contains(key) || !effectiveValues.ContainsKey(key))
                effectiveValues[key] = value;
        }
    }
}
