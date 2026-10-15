using LoipvRemote.WinUI.Services;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Services;

public sealed class PreviousSessionTabsRepositoryTests
{
    [Test]
    public async Task SaveAndLoadPreservesTabOrderAndSelectedConnection()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        string stateFile = Path.Combine(directory, "previous-session-tabs.json");
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        try
        {
            var repository = new PreviousSessionTabsRepository(stateFile);
            var state = PreviousSessionTabsState.Create([Guid.Empty, first, second, first], second);

            await repository.SaveAsync(state);
            PreviousSessionTabsState loaded = await repository.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.ConnectionIds, Is.EqualTo(new[] { first, second }));
                Assert.That(loaded.SelectedConnectionId, Is.EqualTo(second));
            });
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task LoadReturnsEmptyStateWhenFileIsInvalid()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        string stateFile = Path.Combine(directory, "previous-session-tabs.json");
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(stateFile, "not-json");

            var repository = new PreviousSessionTabsRepository(stateFile);
            PreviousSessionTabsState loaded = await repository.LoadAsync();

            Assert.That(loaded, Is.EqualTo(PreviousSessionTabsState.Empty));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
