using LoipvRemote.Infrastructure.Windows.Dpapi;
using LoipvRemote.Application.Configuration;
using LoipvRemote.Application.Credentials;
using LoipvRemote.WinUI.Services;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Services;

public sealed class ConnectionStoreSettingsRepositoryTests
{
    [Test]
    public async Task SaveAsyncRoundTripsAStoreSelectionWithoutPersistingItsPlaintextLocation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        string settingsFile = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        try
        {
            var repository = new ConnectionStoreSettingsRepository(new ReversibleSecretStore(), settingsFile);
            var expected = new ConnectionStoreSettings(
                ConnectionDefinitionStoreKind.SqlServer,
                "Server=database.example;User Id=remote;Password=not-in-json;",
                IsReadOnly: true);

            await repository.SaveAsync(expected);
            ConnectionStoreSettings actual = await repository.LoadAsync();
            string persisted = await File.ReadAllTextAsync(settingsFile);

            Assert.Multiple(() =>
            {
                Assert.That(actual, Is.EqualTo(expected));
                Assert.That(persisted, Does.Not.Contain(expected.Location));
                Assert.That(persisted, Does.Contain("protected:"));
            });
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task LoadAsyncUsesTheCanonicalXmlStoreWhenNoWinUiSettingsExist()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        string settingsFile = Path.Combine(directory, "settings.json");
        try
        {
            var repository = new ConnectionStoreSettingsRepository(new ReversibleSecretStore(), settingsFile);

            ConnectionStoreSettings settings = await repository.LoadAsync();

            Assert.Multiple(() =>
            {
                Assert.That(settings.Kind, Is.EqualTo(ConnectionDefinitionStoreKind.Xml));
                Assert.That(settings.Location, Does.EndWith(Path.Combine("LoipvRemote", "confCons.xml")));
                Assert.That(settings.IsReadOnly, Is.False);
            });
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase(AppThemeMode.System)]
    [TestCase(AppThemeMode.Light)]
    [TestCase(AppThemeMode.Dark)]
    public async Task SaveAsyncRoundTripsTheSelectedTheme(AppThemeMode theme)
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.WinUI.Tests", Guid.NewGuid().ToString("N"));
        string settingsFile = Path.Combine(directory, "settings.json");
        try
        {
            var repository = new ConnectionStoreSettingsRepository(new ReversibleSecretStore(), settingsFile);
            var settings = new ConnectionStoreSettings(ConnectionDefinitionStoreKind.Xml, Path.Combine(directory, "connections.xml"), Theme: theme);

            await repository.SaveAsync(settings);

            Assert.That((await repository.LoadAsync()).Theme, Is.EqualTo(theme));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class ReversibleSecretStore : IStringSecretStore
    {
        public string Protect(string plaintext, string purpose) => "protected:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));

        public string Unprotect(string protectedValue, string purpose) =>
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue["protected:".Length..]));
    }
}
