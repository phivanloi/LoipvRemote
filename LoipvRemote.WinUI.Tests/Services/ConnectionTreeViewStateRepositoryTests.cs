using LoipvRemote.WinUI.Services;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Services;

public sealed class ConnectionTreeViewStateRepositoryTests
{
    [TestCase("{broken")]
    [TestCase("[\"invalid-guid\"]")]
    [TestCase("42")]
    public async Task CorruptExpansionStateDoesNotPreventLoadingConnections(string json)
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string stateFile = Path.Combine(directory, "tree-state.json");
        try
        {
            await File.WriteAllTextAsync(stateFile, json);
            using var repository = new ConnectionTreeViewStateRepository(stateFile);
            Assert.That(await repository.LoadExpandedFolderIdsAsync(), Is.Empty);
            Assert.That(await File.ReadAllTextAsync(stateFile), Is.EqualTo(json));
            var folder = Guid.NewGuid();
            await repository.SaveExpandedFolderIdsAsync(new HashSet<Guid> { folder });
            Assert.That(await repository.LoadExpandedFolderIdsAsync(), Is.EquivalentTo([folder]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task RapidExpansionChangesPersistWithoutTemporaryFileCollisions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        var stateFile = Path.Combine(directory, "tree-state.json");
        var finalFolder = Guid.NewGuid();
        try
        {
            var repository = new ConnectionTreeViewStateRepository(stateFile);
            var writes = Enumerable.Range(0, 30).Select(_ => repository.SaveExpandedFolderIdsAsync(new HashSet<Guid> { Guid.NewGuid() })).ToList();
            writes.Add(repository.SaveExpandedFolderIdsAsync(new HashSet<Guid> { finalFolder }));

            await Task.WhenAll(writes);

            Assert.That(await repository.LoadExpandedFolderIdsAsync(), Is.EquivalentTo([finalFolder]));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task SaveAndLoadRoundTripsOnlyValidExpandedFolderIds()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        string stateFile = Path.Combine(directory, "tree-state.json");
        Guid expanded = Guid.NewGuid();
        try
        {
            var repository = new ConnectionTreeViewStateRepository(stateFile);

            await repository.SaveExpandedFolderIdsAsync(new HashSet<Guid> { Guid.Empty, expanded });
            IReadOnlySet<Guid> loaded = await repository.LoadExpandedFolderIdsAsync();

            Assert.That(loaded, Is.EquivalentTo([expanded]));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
