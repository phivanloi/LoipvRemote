namespace LoipvRemote.WinUI.Sessions;

internal sealed class LocalPowerShellRuntimeLocator
{
    private readonly string _applicationDirectory;
    private readonly string? _programFilesDirectory;
    private readonly string? _path;

    internal LocalPowerShellRuntimeLocator()
        : this(
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetEnvironmentVariable("PATH"))
    {
    }

    internal LocalPowerShellRuntimeLocator(
        string applicationDirectory,
        string? programFilesDirectory,
        string? path)
    {
        _applicationDirectory = Path.GetFullPath(
            applicationDirectory ?? throw new ArgumentNullException(nameof(applicationDirectory)));
        _programFilesDirectory = programFilesDirectory;
        _path = path;
    }

    internal string Resolve()
    {
        IEnumerable<string> candidates = GetCandidates();
        string? executable = candidates.FirstOrDefault(File.Exists);
        return executable ?? throw new FileNotFoundException(
            "The bundled PowerShell 7 runtime is missing. Reinstall Loipv Remote.");
    }

    internal IReadOnlyList<string> GetCandidates()
    {
        var candidates = new List<string>
        {
            Path.Combine(_applicationDirectory, "Runtime", "PowerShell", "pwsh.exe")
        };

        if (!string.IsNullOrWhiteSpace(_programFilesDirectory))
            candidates.Add(Path.Combine(_programFilesDirectory, "PowerShell", "7", "pwsh.exe"));

        if (!string.IsNullOrWhiteSpace(_path))
        {
            candidates.AddRange(_path
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(directory => Path.Combine(directory, "pwsh.exe")));
        }

        return candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
