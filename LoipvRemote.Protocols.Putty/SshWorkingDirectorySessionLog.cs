using System.Text;
using System.Text.RegularExpressions;

namespace LoipvRemote.Protocols.Putty;

/// <summary>
/// Reads the latest visible shell prompt from PuTTY's temporary session-output log when a server
/// does not publish its working directory through the terminal title.
/// </summary>
public static partial class SshWorkingDirectorySessionLogParser
{
    private const int MaximumTailBytes = 64 * 1024;

    public static string? Parse(string? sessionOutput, string? fallbackUsername = null)
    {
        if (string.IsNullOrWhiteSpace(sessionOutput))
            return null;

        string visibleText = StripTerminalControls(sessionOutput);
        string[] lines = visibleText.Split('\n');
        for (int index = lines.Length - 1; index >= 0; index--)
        {
            string line = lines[index].TrimEnd();
            Match match = StandardPrompt().Match(line);
            if (!match.Success)
                match = BracketPrompt().Match(line);
            if (!match.Success)
                continue;

            string username = match.Groups["user"].Value;
            if (string.IsNullOrWhiteSpace(username))
                username = fallbackUsername ?? string.Empty;
            string path = match.Groups["path"].Value.Trim();
            return SshWorkingDirectoryTitleParser.ParseAbsolute(
                $"{username}@session: {path}",
                fallbackUsername);
        }

        return null;
    }

    internal static string? ParseFile(string? path, string? fallbackUsername = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            long offset = Math.Max(0, stream.Length - MaximumTailBytes);
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] bytes = new byte[checked((int)(stream.Length - offset))];
            stream.ReadExactly(bytes);
            return Parse(Encoding.UTF8.GetString(bytes), fallbackUsername);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string StripTerminalControls(string value)
    {
        var result = new StringBuilder(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character == '\u001b')
            {
                SkipEscapeSequence(value, ref index);
                continue;
            }

            if (character == '\b')
            {
                if (result.Length > 0 && result[^1] != '\n')
                    result.Length--;
                continue;
            }

            if (character == '\r')
            {
                if (index + 1 < value.Length && value[index + 1] == '\n')
                    index++;
                result.Append('\n');
                continue;
            }

            if (character == '\n' || !char.IsControl(character))
                result.Append(character);
        }

        return result.ToString();
    }

    private static void SkipEscapeSequence(string value, ref int index)
    {
        if (++index >= value.Length)
            return;

        if (value[index] == '[')
        {
            while (++index < value.Length)
            {
                char candidate = value[index];
                if (candidate is >= '@' and <= '~')
                    return;
            }
            return;
        }

        if (value[index] == ']')
        {
            while (++index < value.Length)
            {
                if (value[index] == '\a')
                    return;
                if (value[index] == '\u001b' && index + 1 < value.Length && value[index + 1] == '\\')
                {
                    index++;
                    return;
                }
            }
        }
    }

    [GeneratedRegex(@"(?:^|\s)(?<user>[A-Za-z0-9._-]+)@(?<host>[^:\s\[\]]+):(?<path>~(?:/[^\r\n#$]*)?|/[^\r\n#$]*?)[#$]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex StandardPrompt();

    [GeneratedRegex(@"(?:^|\s)\[(?<user>[A-Za-z0-9._-]+)@(?<host>[^\s\]]+)\s+(?<path>~(?:/[^\r\n\]#$]*)?|/[^\r\n\]#$]*?)\][#$]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketPrompt();
}

internal static class SshWorkingDirectorySessionLog
{
    private const string FilePrefix = "ssh-";
    private static readonly string LogDirectory = Path.Combine(
        Path.GetTempPath(),
        "LoipvRemote",
        "SessionLogs");
    private static int _cleanupAttempted;

    public static string CreatePath()
    {
        Directory.CreateDirectory(LogDirectory);
        CleanupStaleLogsOnce();
        return Path.Combine(LogDirectory, $"{FilePrefix}{Guid.NewGuid():N}.log");
    }

    public static void DeleteIfOwned(string? path)
    {
        if (!IsOwnedPath(path))
            return;

        try
        {
            File.Delete(path!);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsOwnedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return string.Equals(Path.GetDirectoryName(fullPath), LogDirectory, StringComparison.OrdinalIgnoreCase) &&
               Path.GetFileName(fullPath).StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Path.GetExtension(fullPath), ".log", StringComparison.OrdinalIgnoreCase);
    }

    private static void CleanupStaleLogsOnce()
    {
        if (Interlocked.Exchange(ref _cleanupAttempted, 1) != 0)
            return;

        try
        {
            DateTime cutoff = DateTime.UtcNow.AddDays(-1);
            foreach (string path in Directory.EnumerateFiles(LogDirectory, $"{FilePrefix}*.log"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < cutoff)
                        File.Delete(path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
