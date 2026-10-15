using System.Text.Json;

namespace LoipvRemote.WinUI.Services;

/// <summary>Persists only folder expansion state; it never stores connection secrets or definitions.</summary>
public sealed class ConnectionTreeViewStateRepository
{
    private readonly string _filePath;

    public ConnectionTreeViewStateRepository()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LoipvRemote", "tree-state.json"))
    {
    }

    internal ConnectionTreeViewStateRepository(string filePath) => _filePath = Path.GetFullPath(filePath);

    public async Task<IReadOnlySet<Guid>> LoadExpandedFolderIdsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return new HashSet<Guid>();
        await using FileStream stream = File.OpenRead(_filePath);
        Guid[] ids = await JsonSerializer.DeserializeAsync<Guid[]>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? [];
        return ids.Where(id => id != Guid.Empty).ToHashSet();
    }

    public async Task SaveExpandedFolderIdsAsync(IReadOnlySet<Guid> folderIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folderIds);
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        string temporaryPath = _filePath + ".tmp";
        try
        {
            await using (FileStream stream = File.Create(temporaryPath))
                await JsonSerializer.SerializeAsync(stream, folderIds.OrderBy(id => id).ToArray(), cancellationToken: cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
