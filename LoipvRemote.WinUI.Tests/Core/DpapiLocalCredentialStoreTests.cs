using LoipvRemote.Domain.Credentials;
using LoipvRemote.Infrastructure.Windows.Dpapi;
using LoipvRemote.Application.Credentials;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Core;

public sealed class DpapiLocalCredentialStoreTests
{
    [Test]
    public async Task LoadsCredentialsWrittenWithCamelCasePropertyNames()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "credentials.json");
        try
        {
            Directory.CreateDirectory(directory);
            Guid id = Guid.NewGuid();
            string protectedPassword = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test-password"));
            await File.WriteAllTextAsync(path, $$"""
                [
                  {
                    "id": "{{id:D}}",
                    "name": "Production",
                    "userName": "administrator",
                    "protectedPassword": "{{protectedPassword}}"
                  }
                ]
                """);

            var store = new DpapiLocalCredentialStore(new ReversibleSecretStore(), path);

            Assert.That(await store.ListAsync(), Is.EqualTo(new[] { new LocalCredentialDefinition(id, "Production", "administrator") }));
            Assert.That(store.ResolvePassword(CredentialReference.LocalDpapi(id)), Is.EqualTo("test-password"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task SaveListResolveAndDeleteRoundTripWithoutWritingPlaintext()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoipvRemote.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "credentials.json");
        try
        {
            Guid id = Guid.NewGuid();
            var credential = new LocalCredentialDefinition(id, "Production", "administrator");
            var store = new DpapiLocalCredentialStore(new ReversibleSecretStore(), path);

            await store.SaveAsync(credential, "super-secret");

            Assert.That(await store.ListAsync(), Is.EqualTo(new[] { credential }));
            Assert.That(await File.ReadAllTextAsync(path), Does.Not.Contain("super-secret"));

            var reopened = new DpapiLocalCredentialStore(new ReversibleSecretStore(), path);
            Assert.That(await reopened.ListAsync(), Is.EqualTo(new[] { credential }));
            Assert.That(reopened.ResolvePassword(CredentialReference.LocalDpapi(id)), Is.EqualTo("super-secret"));

            await reopened.DeleteAsync(id);
            Assert.That(await reopened.ListAsync(), Is.Empty);
            Assert.That(reopened.ResolvePassword(CredentialReference.LocalDpapi(id)), Is.Null);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class ReversibleSecretStore : IStringSecretStore
    {
        public string Protect(string plaintext, string purpose) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));

        public string Unprotect(string protectedValue, string purpose) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue));
    }
}
