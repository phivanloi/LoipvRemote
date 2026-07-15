namespace LoipvRemote.Protocols.Putty;

/// <summary>Resolves the PuTTY executable used by the protocol factory.</summary>
public interface IPuttyExecutableLocator
{
    string? Locate();
}

/// <summary>Locates PuTTY from the standard installation folders or PATH.</summary>
public sealed class SystemPuttyExecutableLocator : IPuttyExecutableLocator
{
    public string? Locate()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PuTTY", "putty.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PuTTY", "putty.exe")
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim(), "putty.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
