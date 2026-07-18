using System.Text;

namespace LoipvRemote.Protocols.Putty;

public static class PuttyLaunchArguments
{
    public static string Build(PuttyLaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> arguments = [];
        Add(arguments, "-load", options.SavedSession);

        if (!options.UseSavedSessionOnly)
        {
            Add(arguments, "-" + options.Protocol.ToString().ToLowerInvariant());

            if (options.Protocol == PuttyProtocolKind.Ssh)
            {
                Add(arguments, "-" + (int)options.SshVersion);
                if (!options.SuppressCredentials)
                {
                    if (!string.IsNullOrEmpty(options.Username))
                        Add(arguments, "-l", options.Username);
                    if (!string.IsNullOrEmpty(options.PasswordPipeName))
                        Add(arguments, "-pwfile", $"\\\\.\\PIPE\\{options.PasswordPipeName}");
                }

                if (!string.IsNullOrEmpty(options.PrivateKeyPath))
                    Add(arguments, "-i", options.PrivateKeyPath);
                if (!string.IsNullOrEmpty(options.OpeningCommandPath))
                    Add(arguments, "-m", options.OpeningCommandPath);
                if (!string.IsNullOrEmpty(options.AuthenticationPluginCommand))
                    Add(arguments, "-auth-plugin", options.AuthenticationPluginCommand);

            }

            Add(arguments, "-P", options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Add(arguments, options.Hostname);
        }

        string result = string.Join(' ', arguments);
        if (!string.IsNullOrWhiteSpace(options.AdditionalOptions))
            result += " " + options.AdditionalOptions.Trim();
        return result;
    }

    private static void Add(List<string> destination, params string[] values)
    {
        foreach (string value in values)
            destination.Add(Quote(value));
    }

    private static string Quote(string value)
    {
        if (value.Length > 0 && !value.Any(char.IsWhiteSpace) && !value.Contains('"'))
            return value;

        StringBuilder result = new(value.Length + 2);
        result.Append('"');
        int backslashes = 0;
        foreach (char character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                result.Append('\\', backslashes * 2 + 1);
                result.Append('"');
                backslashes = 0;
                continue;
            }

            result.Append('\\', backslashes);
            backslashes = 0;
            result.Append(character);
        }

        result.Append('\\', backslashes * 2);
        result.Append('"');
        return result.ToString();
    }
}
