using System.Text.Json;

namespace LoipvRemote.WinUI.Services;

/// <summary>Stores only connection IDs and tab order from the last clean shutdown.</summary>
public sealed class PreviousSessionTabsRepository
{
    private readonly string _filePath;

    public PreviousSessionTabsRepository()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LoipvRemote",
            "previous-session-tabs.json"))
    {
    }

    internal PreviousSessionTabsRepository(string filePath) =>
        _filePath = Path.GetFullPath(filePath);

    public async Task<PreviousSessionTabsState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return PreviousSessionTabsState.Empty;

        try
        {
            await using FileStream stream = File.OpenRead(_filePath);
            PersistedPreviousSessionTabs? persisted = await JsonSerializer.DeserializeAsync<PersistedPreviousSessionTabs>(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return persisted is null
                ? PreviousSessionTabsState.Empty
                : PreviousSessionTabsState.Create(persisted.ConnectionIds ?? [], persisted.SelectedConnectionId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return PreviousSessionTabsState.Empty;
        }
    }

    public async Task SaveAsync(
        PreviousSessionTabsState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        PreviousSessionTabsState normalized = PreviousSessionTabsState.Create(
            state.ConnectionIds,
            state.SelectedConnectionId);
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = _filePath + ".tmp";
        try
        {
            await using (FileStream stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new PersistedPreviousSessionTabs(
                        normalized.ConnectionIds.ToArray(),
                        normalized.SelectedConnectionId),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private sealed record PersistedPreviousSessionTabs(
        Guid[]? ConnectionIds,
        Guid? SelectedConnectionId);
}

public sealed record PreviousSessionTabsState(
    IReadOnlyList<Guid> ConnectionIds,
    Guid? SelectedConnectionId)
{
    public static PreviousSessionTabsState Empty { get; } = new([], null);

    public static PreviousSessionTabsState Create(
        IEnumerable<Guid> connectionIds,
        Guid? selectedConnectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionIds);
        Guid[] normalizedIds = connectionIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (normalizedIds.Length == 0)
            return Empty;

        Guid? normalizedSelection = selectedConnectionId is Guid selected &&
            normalizedIds.Contains(selected)
                ? selected
                : null;
        return new PreviousSessionTabsState(normalizedIds, normalizedSelection);
    }
}
