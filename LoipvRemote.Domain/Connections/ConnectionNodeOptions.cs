namespace LoipvRemote.Domain.Connections;

/// <summary>
/// Secret-free protocol and display settings plus the inherited property names
/// for one connection-tree node. Values use invariant strings so Domain stays
/// independent from desktop UI enum types.
/// </summary>
public sealed record ConnectionNodeOptions(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyCollection<string> InheritedProperties)
{
    private static readonly HashSet<string> SecretPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "RDGatewayPassword",
        "RDGatewayAccessToken",
        "VNCProxyPassword"
    };

    public static ConnectionNodeOptions Empty { get; } = new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        Array.Empty<string>());

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Values);
        ArgumentNullException.ThrowIfNull(InheritedProperties);

        foreach ((string name, string value) in Values)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Connection option names are required.", nameof(Values));
            if (SecretPropertyNames.Contains(name))
                throw new ArgumentException($"Connection option '{name}' must be represented by a credential reference.", nameof(Values));
            ArgumentNullException.ThrowIfNull(value);
        }

        if (InheritedProperties.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Inherited property names are required.", nameof(InheritedProperties));
        if (InheritedProperties.Distinct(StringComparer.Ordinal).Count() != InheritedProperties.Count)
            throw new ArgumentException("Inherited property names must be unique.", nameof(InheritedProperties));
    }
}
