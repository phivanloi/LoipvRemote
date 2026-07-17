using LoipvRemote.Domain.Connections;
using LoipvRemote.Application.Credentials;

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

    public static string Format(ConnectionNodeOptions? options)
    {
        if (options is null)
            return string.Empty;

        return string.Join(Environment.NewLine, options.Values
            .Where(pair => !pair.Key.StartsWith(ProtectedSecretPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
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
