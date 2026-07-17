using LoipvRemote.Domain.Connections;
using LoipvRemote.Application.Credentials;
using LoipvRemote.WinUI.Services;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Services;

public sealed class ConnectionOptionsEditorTests
{
    [Test]
    public void BuildPersistsPasswordAsAConnectionScopedProtectedOption()
    {
        Guid connectionId = Guid.NewGuid();
        var editor = new ConnectionOptionsEditor(new RecordingSecretStore());

        ConnectionNodeOptions? options = editor.Build(connectionId, "Username=admin\nSmartSize=true", "secret", clearStoredPassword: false);

        Assert.Multiple(() =>
        {
            Assert.That(options, Is.Not.Null);
            Assert.That(options!.Values["Username"], Is.EqualTo("admin"));
            Assert.That(options.Values["$dpapi-secret:Password"], Is.EqualTo("protected:secret"));
            Assert.That(ConnectionOptionsEditor.Format(options), Does.Not.Contain("secret"));
        });
    }

    [Test]
    public void BuildKeepsExistingProtectedPasswordWhenPasswordFieldIsBlank()
    {
        var existing = new ConnectionNodeOptions(
            new Dictionary<string, string> { ["$dpapi-secret:Password"] = "protected:existing", ["Username"] = "old" },
            ["Username"]);

        ConnectionNodeOptions? options = new ConnectionOptionsEditor(new RecordingSecretStore())
            .Build(Guid.NewGuid(), "Username=new", string.Empty, clearStoredPassword: false, existingOptions: existing);

        Assert.Multiple(() =>
        {
            Assert.That(options!.Values["Username"], Is.EqualTo("new"));
            Assert.That(options.Values["$dpapi-secret:Password"], Is.EqualTo("protected:existing"));
            Assert.That(options.InheritedProperties, Is.EqualTo(["Username"]));
        });
    }

    [Test]
    public void BuildRejectsMalformedOrReservedOptionLines()
    {
        var editor = new ConnectionOptionsEditor(new RecordingSecretStore());

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => editor.Build(Guid.NewGuid(), "Username", null, false));
            Assert.Throws<ArgumentException>(() => editor.Build(Guid.NewGuid(), "$dpapi-secret:Password=value", null, false));
        });
    }

    private sealed class RecordingSecretStore : IStringSecretStore
    {
        public string Protect(string plaintext, string purpose) => "protected:" + plaintext;

        public string Unprotect(string protectedValue, string purpose) => protectedValue["protected:".Length..];
    }
}
