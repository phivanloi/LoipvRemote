using System.Text;

namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Parses a Windows command line into arguments for ProcessStartInfo.ArgumentList.</summary>
public static class ExternalApplicationCommandLine
{
    public static IReadOnlyList<string> SplitArguments(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return Array.Empty<string>();

        List<string> arguments = [];
        int index = 0;

        while (index < commandLine.Length)
        {
            SkipWhitespace(commandLine, ref index);
            if (index >= commandLine.Length)
                break;

            StringBuilder argument = new();
            bool inQuotes = false;
            while (index < commandLine.Length)
            {
                char current = commandLine[index];
                if (!inQuotes && char.IsWhiteSpace(current))
                    break;
                if (current != '\\')
                {
                    if (current == '"') inQuotes = !inQuotes;
                    else argument.Append(current);
                    index++;
                    continue;
                }

                int slashCount = 0;
                while (index < commandLine.Length && commandLine[index] == '\\')
                {
                    slashCount++;
                    index++;
                }

                if (index < commandLine.Length && commandLine[index] == '"')
                {
                    argument.Append('\\', slashCount / 2);
                    if (slashCount % 2 == 0) inQuotes = !inQuotes;
                    else argument.Append('"');
                    index++;
                }
                else
                {
                    argument.Append('\\', slashCount);
                }
            }

            arguments.Add(argument.ToString());
            SkipWhitespace(commandLine, ref index);
        }

        return arguments;
    }

    private static void SkipWhitespace(string value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
            index++;
    }
}
