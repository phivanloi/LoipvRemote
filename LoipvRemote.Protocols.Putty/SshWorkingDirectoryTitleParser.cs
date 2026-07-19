namespace LoipvRemote.Protocols.Putty;

/// <summary>Extracts a shell working directory from PuTTY's remotely controlled window title.</summary>
public static class SshWorkingDirectoryTitleParser
{
    private const string Marker = "LoipvRemote:CWD:";
    private const int MaximumPathLength = 4096;

    public static string? Parse(string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle) || windowTitle.Length > MaximumPathLength + 256)
            return null;

        string candidate;
        if (windowTitle.StartsWith(Marker, StringComparison.Ordinal))
        {
            try
            {
                candidate = Uri.UnescapeDataString(windowTitle[Marker.Length..]);
            }
            catch (UriFormatException)
            {
                return null;
            }
        }
        else
        {
            int separator = windowTitle.LastIndexOf(": ", StringComparison.Ordinal);
            if (separator < 0)
                return null;
            candidate = windowTitle[(separator + 2)..];
        }

        candidate = candidate.Trim();
        if (candidate.Length is 0 or > MaximumPathLength ||
            candidate.Any(char.IsControl) ||
            (candidate[0] != '/' && candidate[0] != '~'))
        {
            return null;
        }

        if (candidate[0] == '~' && candidate.Length > 1 && candidate[1] != '/')
            return null;

        return candidate;
    }

    /// <summary>Returns the title working directory in canonical absolute form.</summary>
    public static string? ParseAbsolute(string? windowTitle, string? fallbackUsername = null)
    {
        string? path = Parse(windowTitle);
        if (path is null || path[0] == '/')
            return path;

        string? titleUsername = ParseUsername(windowTitle);
        string? username = string.IsNullOrWhiteSpace(titleUsername) ? fallbackUsername : titleUsername;
        if (string.IsNullOrWhiteSpace(username))
            return null;

        string home = string.Equals(username, "root", StringComparison.Ordinal)
            ? "/root"
            : $"/home/{username}";
        return path == "~" ? home : $"{home}/{path[2..]}";
    }

    private static string? ParseUsername(string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle) || windowTitle.StartsWith(Marker, StringComparison.Ordinal))
            return null;

        int at = windowTitle.IndexOf('@');
        int separator = windowTitle.LastIndexOf(": ", StringComparison.Ordinal);
        if (at <= 0 || at > separator)
            return null;

        string username = windowTitle[..at].Trim();
        return username.Length > 0 && username.All(character =>
            !char.IsWhiteSpace(character) && !char.IsControl(character) && character is not '/' and not '\\')
            ? username
            : null;
    }
}
