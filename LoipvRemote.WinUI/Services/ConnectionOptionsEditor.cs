using LoipvRemote.Application.Credentials;
using LoipvRemote.Application.Sessions;
using LoipvRemote.Domain.Connections;

namespace LoipvRemote.WinUI.Services;

/// <summary>
/// Converts the advanced-options editor text into a secret-free Domain options
/// bag. Passwords never enter XML or the UI state as plaintext: they are held
/// in the current-user DPAPI store under the connection-specific purpose.
/// </summary>
public sealed class ConnectionOptionsEditor(IStringSecretStore secretStore)
{
    private const string ProtectedSecretPrefix = "$dpapi-secret:";
    private readonly IStringSecretStore _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));

    /// <summary>Tạo kết nối tạm và bảo vệ password theo định danh của session.</summary>
    internal ConnectionDefinition CreateQuickConnection(string host, int port, ProtocolKind protocol, string username, string? password)
    {
        var connection = QuickConnectionDefinitionFactory.Create(host, port, protocol);
        var options = Build(connection.Id, string.Empty, password, clearStoredPassword: false);
        return connection with { Options = WithOptionValue(options, "Username", username) };
    }

    public ConnectionNodeOptions? Build(
        Guid connectionId,
        string serializedOptions,
        string? password,
        bool clearStoredPassword,
        ConnectionNodeOptions? existingOptions = null)
    {
        if (connectionId == Guid.Empty)
            throw new ArgumentException("A connection id is required.", nameof(connectionId));

        Dictionary<string, string> values = Parse(serializedOptions);
        if (!clearStoredPassword && string.IsNullOrEmpty(password) && existingOptions is not null)
        {
            foreach ((string key, string value) in existingOptions.Values.Where(pair => pair.Key.StartsWith(ProtectedSecretPrefix, StringComparison.OrdinalIgnoreCase)))
                values[key] = value;
        }

        if (!string.IsNullOrEmpty(password))
        {
            string purpose = ConnectionSecretPurposes.ForConnectionOption(connectionId.ToString("D"), "Password");
            values[ProtectedSecretPrefix + "Password"] = _secretStore.Protect(password, purpose);
        }

        IReadOnlyCollection<string> inherited = existingOptions?.InheritedProperties ?? [];
        return values.Count == 0 && inherited.Count == 0
            ? null
            : new ConnectionNodeOptions(values, inherited.ToArray());
    }

    public static string Format(ConnectionNodeOptions? options, params string[] excludedOptionNames)
    {
        if (options is null)
            return string.Empty;

        var excluded = new HashSet<string>(excludedOptionNames, StringComparer.OrdinalIgnoreCase);
        return string.Join(Environment.NewLine, options.Values
            .Where(pair => !pair.Key.StartsWith(ProtectedSecretPrefix, StringComparison.OrdinalIgnoreCase) && !excluded.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
    }

    /// <summary>Updates a first-class editor field without exposing it as an advanced option.</summary>
    public static ConnectionNodeOptions? WithOptionValue(ConnectionNodeOptions? options, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An option name is required.", nameof(name));

        var values = options is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(options.Values, StringComparer.Ordinal);
        string? existingName = values.Keys.FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));
        if (existingName is not null)
            values.Remove(existingName);
        if (!string.IsNullOrWhiteSpace(value))
            values[name] = value.Trim();

        IReadOnlyCollection<string> inherited = options?.InheritedProperties ?? [];
        return values.Count == 0 && inherited.Count == 0
            ? null
            : new ConnectionNodeOptions(values, inherited.ToArray());
    }

    /// <summary>Re-protects connection-scoped secrets when a definition receives a new identity.</summary>
    public ConnectionNodeOptions? ReprotectSecrets(
        ConnectionDefinition source,
        Guid targetConnectionId)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (targetConnectionId == Guid.Empty)
            throw new ArgumentException("A connection ID is required.", nameof(targetConnectionId));
        if (source.Options is null)
            return null;

        Dictionary<string, string> values = new(source.Options.Values, StringComparer.Ordinal);
        foreach ((string key, string protectedValue) in source.Options.Values
                     .Where(pair => pair.Key.StartsWith(ProtectedSecretPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            string propertyName = key[ProtectedSecretPrefix.Length..];
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new InvalidDataException("A protected connection option has no property name.");

            string plaintext = _secretStore.Unprotect(
                protectedValue,
                ConnectionSecretPurposes.ForConnectionOption(source.Id.ToString("D"), propertyName));
            values[key] = _secretStore.Protect(
                plaintext,
                ConnectionSecretPurposes.ForConnectionOption(targetConnectionId.ToString("D"), propertyName));
        }

        return new ConnectionNodeOptions(values, source.Options.InheritedProperties.ToArray());
    }

    private static Dictionary<string, string> Parse(string serializedOptions)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(serializedOptions))
            return values;

        string[] lines = serializedOptions.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (line.Length == 0)
                continue;

            int separator = line.IndexOf('=');
            if (separator <= 0)
                throw new ArgumentException($"Option line {index + 1} must use Name=Value format.", nameof(serializedOptions));

            string name = line[..separator].Trim();
            string value = line[(separator + 1)..];
            if (name.StartsWith(ProtectedSecretPrefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Protected option names are managed by LoipvRemote and cannot be entered manually.", nameof(serializedOptions));
            if (!values.TryAdd(name, value))
                throw new ArgumentException($"Option '{name}' is listed more than once.", nameof(serializedOptions));
        }

        return values;
    }
}
