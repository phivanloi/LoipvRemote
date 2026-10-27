using System.Text.Json;

namespace LoipvRemote.WinUI.Services;

/// <summary>Persists only folder expansion state; it never stores connection secrets or definitions.</summary>
public sealed class ConnectionTreeViewStateRepository : IDisposable
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public ConnectionTreeViewStateRepository()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LoipvRemote", "tree-state.json"))
    {
    }

    internal ConnectionTreeViewStateRepository(string filePath) => _filePath = Path.GetFullPath(filePath);

    public async Task<IReadOnlySet<Guid>> LoadExpandedFolderIdsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return new HashSet<Guid>();
        try
        {
            await using FileStream stream = File.OpenRead(_filePath);
            Guid[] ids = await JsonSerializer.DeserializeAsync<Guid[]>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? [];
            return ids.Where(id => id != Guid.Empty).ToHashSet();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Trạng thái mở rộng chỉ hỗ trợ hiển thị; lỗi ở đây không được che cây connection.
            return new HashSet<Guid>();
        }
    }

    public async Task SaveExpandedFolderIdsAsync(IReadOnlySet<Guid> folderIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folderIds);
        var snapshot = folderIds.Where(id => id != Guid.Empty).OrderBy(id => id).ToArray();
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var temporaryPath = _filePath + ".tmp";
            try
            {
                await using (var stream = File.Create(temporaryPath))
                    await JsonSerializer.SerializeAsync(stream, snapshot, cancellationToken: cancellationToken).ConfigureAwait(false);
                File.Move(temporaryPath, _filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
        finally
        {
            _saveGate.Release();
        }
    }

    /// <summary>Giải phóng bộ đồng bộ ghi trạng thái cây.</summary>
    public void Dispose()
    {
        _saveGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
