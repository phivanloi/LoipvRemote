namespace LoipvRemote.Infrastructure.Windows.Process;

public static class AnyDeskExecutableLocator
{
    private static readonly string[] DefaultPaths =
    [
        @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe",
        @"C:\Program Files\AnyDesk\AnyDesk.exe"
    ];

    public static string? Find(string? pathEnvironment = null)
    {
        foreach (string path in DefaultPaths)
        {
            if (File.Exists(path))
                return path;
        }

        string? value = pathEnvironment ?? Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(value))
            return null;

        foreach (string directory in value.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim(), "AnyDesk.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
