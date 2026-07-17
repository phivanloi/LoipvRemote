using LoipvRemote.WinUI.Services;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Services;

public sealed class ConnectionTreeViewStateRepositoryTests
{
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
