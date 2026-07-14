namespace LoipvRemote.Protocols.ExternalApps;

/// <summary>
/// Expands connection and environment variables in configured external application arguments.
/// </summary>
public sealed class ExternalApplicationArgumentParser(ExternalApplicationArgumentContext? context)
{
    private readonly ExternalApplicationArgumentContext? _context = context;

    public string ParseArguments(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        List<Replacement> replacements = BuildReplacementList(input);
        return PerformReplacements(input, replacements);
    }

    private List<Replacement> BuildReplacementList(string input)
    {
        int index = 0;
        List<Replacement> replacements = [];
        do
        {
            int tokenStart = input.IndexOf('%', index);
            if (tokenStart == -1)
                break;

            int tokenEnd = input.IndexOf('%', tokenStart + 1);
            if (tokenEnd == -1)
                break;

            int tokenLength = tokenEnd - tokenStart + 1;
            int variableNameStart = tokenStart + 1;
            int variableNameLength = tokenLength - 2;
            bool isEnvironmentVariable = false;
            string variableName = string.Empty;

            if (tokenStart > 0)
            {
                char tokenStartPrefix = input[tokenStart - 1];
                char tokenEndPrefix = input[tokenEnd - 1];

                if (tokenStartPrefix == '\\' && tokenEndPrefix == '\\')
                {
                    isEnvironmentVariable = true;
                    tokenStart--;
                    tokenLength++;
                    variableNameLength--;
                }
                else if (tokenStartPrefix == '^' && tokenEndPrefix == '^')
                {
                    tokenStart--;
                    tokenLength++;
                    variableNameLength--;

                    variableName = input.Substring(variableNameStart, variableNameLength);
                    replacements.Add(new Replacement(tokenStart, tokenLength, $"%{variableName}%"));
                    index = tokenEnd;
                    continue;
                }
            }

            string token = input.Substring(tokenStart, tokenLength);
            EscapeType escape = DetermineEscapeType(token);

            if (escape != EscapeType.All)
            {
                variableNameStart++;
                variableNameLength--;
            }

            if (variableNameLength == 0)
            {
                index = tokenEnd;
                continue;
            }

            variableName = input.Substring(variableNameStart, variableNameLength);
            string replacementValue = isEnvironmentVariable ? token : GetVariableReplacement(variableName, token);
            bool haveReplacement = replacementValue != token;

            if (!haveReplacement)
            {
                replacementValue = Environment.GetEnvironmentVariable(variableName);
                haveReplacement = replacementValue is not null;
            }

            if (haveReplacement)
            {
                char trailing = tokenEnd + 2 <= input.Length ? input[tokenEnd + 1] : '\0';
                if (escape == EscapeType.All)
                {
                    replacementValue = EscapeBackslashes(replacementValue!);
                    if (trailing == '\'')
                        replacementValue = EscapeBackslashesForTrailingQuote(replacementValue);
                }

                if (escape is EscapeType.All or EscapeType.ShellMetacharacters)
                    replacementValue = EscapeShellMetacharacters(replacementValue!);

                replacements.Add(new Replacement(tokenStart, tokenLength, replacementValue!));
                index = tokenEnd + 1;
            }
            else
            {
                index = tokenEnd;
            }
        } while (true);

        return replacements;
    }

    private static EscapeType DetermineEscapeType(string token) => token[1] switch
    {
        '-' => EscapeType.ShellMetacharacters,
        '!' => EscapeType.None,
        _ => EscapeType.All
    };

    private string GetVariableReplacement(string variable, string original)
    {
        if (_context is null)
            return string.Empty;

        return variable.ToLowerInvariant() switch
        {
            "name" => _context.Name ?? string.Empty,
            "hostname" => _context.Hostname ?? string.Empty,
            "port" => Convert.ToString(_context.Port),
            "username" => _context.Username ?? string.Empty,
            "password" => _context.Password ?? string.Empty,
            "domain" => _context.Domain ?? string.Empty,
            "description" => _context.Description ?? string.Empty,
            "macaddress" => _context.MacAddress ?? string.Empty,
            "userfield" => _context.UserField ?? string.Empty,
            _ => original
        };
    }

    private static string PerformReplacements(string input, List<Replacement> replacements)
    {
        string result = input;
        for (int index = result.Length; index >= 0; index--)
        {
            foreach (Replacement replacement in replacements.Where(replacement => replacement.Start == index))
            {
                string before = result[..replacement.Start];
                string after = result[(replacement.Start + replacement.Length)..];
                result = before + replacement.Value + after;
            }
        }

        return result;
    }

    private static string EscapeBackslashes(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : System.Text.RegularExpressions.Regex.Replace(value, "(\\\\*)\"", "$1$1\\\"");

    private static string EscapeBackslashesForTrailingQuote(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : System.Text.RegularExpressions.Regex.Replace(value, "(\\\\*)$", "$1$1");

    private static string EscapeShellMetacharacters(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : System.Text.RegularExpressions.Regex.Replace(value, "([()%!^\"<>&|])", "^$1");

    private enum EscapeType
    {
        All,
        ShellMetacharacters,
        None
    }

    private readonly record struct Replacement(int Start, int Length, string Value);
}
