using System.Text;

namespace LoipvRemote.Protocols.ExternalApps;

public static class ProcessArgumentEscaper
{
    public static string Quote(string value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && value.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0)
            return value;

        StringBuilder result = new();
        result.Append('"');
        int backslashes = 0;
        foreach (char character in value)
        {
            if (character == '\\') { backslashes++; continue; }
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
