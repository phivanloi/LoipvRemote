using System.Globalization;
using System.Text.RegularExpressions;

namespace LoipvRemote.ApplicationServices.Configuration;

/// <summary>Expands connection and environment variables in configured external-tool arguments.</summary>
public sealed class ExternalApplicationArgumentParser(ExternalApplicationArgumentContext? context)
{
    public string ParseArguments(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        List<Replacement> replacements = BuildReplacementList(input);
        string result = input;
        for (int index = result.Length; index >= 0; index--)
        {
            foreach (Replacement replacement in replacements.Where(item => item.Start == index))
                result = result[..replacement.Start] + replacement.Value + result[(replacement.Start + replacement.Length)..];
        }
        return result;
    }

    private List<Replacement> BuildReplacementList(string input)
    {
        List<Replacement> replacements = [];
        int index = 0;
        while (index < input.Length)
        {
            int tokenStart = input.IndexOf('%', index);
            if (tokenStart < 0)
                break;
            int tokenEnd = input.IndexOf('%', tokenStart + 1);
            if (tokenEnd < 0)
                break;

            int tokenLength = tokenEnd - tokenStart + 1;
            int variableNameStart = tokenStart + 1;
            int variableNameLength = tokenLength - 2;
            bool isEnvironmentVariable = false;
            if (tokenStart > 0)
            {
                char startPrefix = input[tokenStart - 1];
                char endPrefix = input[tokenEnd - 1];
                if (startPrefix == '\\' && endPrefix == '\\')
                {
                    isEnvironmentVariable = true;
                    tokenStart--;
                    tokenLength++;
                    variableNameLength--;
                }
                else if (startPrefix == '^' && endPrefix == '^')
                {
                    tokenStart--;
                    tokenLength++;
                    variableNameLength--;
                    string escapedVariable = input.Substring(variableNameStart, variableNameLength);
                    replacements.Add(new Replacement(tokenStart, tokenLength, $"%{escapedVariable}%"));
                    index = tokenEnd;
                    continue;
                }
            }

            string token = input.Substring(tokenStart, tokenLength);
            EscapeType escape = token[1] switch
            {
                '-' => EscapeType.ShellMetacharacters,
                '!' => EscapeType.None,
                _ => EscapeType.All
            };
            if (escape != EscapeType.All)
            {
                variableNameStart++;
                variableNameLength--;
            }
            if (variableNameLength <= 0)
            {
                index = tokenEnd;
                continue;
            }

            string variable = input.Substring(variableNameStart, variableNameLength);
            string? replacement = isEnvironmentVariable ? token : GetVariableReplacement(variable, token);
            if (replacement == token)
                replacement = Environment.GetEnvironmentVariable(variable);
            if (replacement is null)
            {
                index = tokenEnd;
                continue;
            }

            char trailing = tokenEnd + 1 < input.Length ? input[tokenEnd + 1] : '\0';
            if (escape == EscapeType.All)
            {
                replacement = EscapeBackslashes(replacement);
                if (trailing == '\'')
                    replacement = EscapeBackslashesForTrailingQuote(replacement);
            }
            if (escape is EscapeType.All or EscapeType.ShellMetacharacters)
                replacement = EscapeShellMetacharacters(replacement);
            replacements.Add(new Replacement(tokenStart, tokenLength, replacement));
            index = tokenEnd + 1;
        }
        return replacements;
    }

    private string GetVariableReplacement(string variable, string original) =>
        context is null
            ? string.Empty
            : variable.ToLowerInvariant() switch
            {
                "name" => context.Name ?? string.Empty,
                "hostname" => context.Hostname ?? string.Empty,
                "port" => context.Port.ToString(CultureInfo.InvariantCulture),
                "username" => context.Username ?? string.Empty,
                "password" => context.Password ?? string.Empty,
                "domain" => context.Domain ?? string.Empty,
                "description" => context.Description ?? string.Empty,
                "macaddress" => context.MacAddress ?? string.Empty,
                "userfield" => context.UserField ?? string.Empty,
                _ => original
            };

    private static string EscapeBackslashes(string value) =>
        string.IsNullOrEmpty(value) ? value : Regex.Replace(value, "(\\\\*)\\\"", "$1$1\\\"");

    private static string EscapeBackslashesForTrailingQuote(string value) =>
        string.IsNullOrEmpty(value) ? value : Regex.Replace(value, "(\\\\*)$", "$1$1");

    private static string EscapeShellMetacharacters(string value) =>
        string.IsNullOrEmpty(value) ? value : Regex.Replace(value, "([()%!^\"<>&|])", "^$1");

    private enum EscapeType { All, ShellMetacharacters, None }
    private readonly record struct Replacement(int Start, int Length, string Value);
}
