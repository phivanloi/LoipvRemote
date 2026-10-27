using LoipvRemote.Application.Credentials;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Infrastructure.Windows.Dpapi;
using LoipvRemote.WinUI.Services;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Services;

public sealed class ConnectionOptionsEditorTests
{
    [Test]
    public void QuickConnectionPasswordCanBeDecryptedWithTheReturnedSessionIdentity()
    {
        var secrets = new DpapiStringSecretStore(new WindowsDpapiSecretProtector());
        var editor = new ConnectionOptionsEditor(secrets);

        var connection = editor.CreateQuickConnection("127.0.0.1", 22220, ProtocolKind.Ssh2, "e2e", "test-only");
        var protectedPassword = connection.Options!.Values["$dpapi-secret:Password"];

        Assert.That(secrets.Unprotect(protectedPassword,
            ConnectionSecretPurposes.ForConnectionOption(connection.Id.ToString("D"), "Password")), Is.EqualTo("test-only"));
    }

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

    [Test]
    public void FirstClassUsernameFieldIsExcludedFromAdvancedTextAndCanBeUpdated()
    {
        var options = new ConnectionNodeOptions(
            new Dictionary<string, string> { ["Username"] = "old-admin", ["SmartSize"] = "true" },
            []);

        string advancedText = ConnectionOptionsEditor.Format(options, "Username");
        ConnectionNodeOptions? updated = ConnectionOptionsEditor.WithOptionValue(options, "Username", "operator");

        Assert.Multiple(() =>
        {
            Assert.That(advancedText, Is.EqualTo("SmartSize=true"));
            Assert.That(updated!.Values["Username"], Is.EqualTo("operator"));
            Assert.That(updated.Values["SmartSize"], Is.EqualTo("true"));
        });
    }

    [Test]
    public void StructuredFieldsPreserveHiddenOptionsSecretsAndCustomDriveSelection()
    {
        var original = new ConnectionNodeOptions(new Dictionary<string, string>
        {
            ["PrivateKeyPath"] = "%USERPROFILE%\\.ssh\\test.ppk",
            ["RedirectDiskDrives"] = "Custom",
            ["RedirectDiskDrivesCustom"] = "D,E",
            ["SSHOptions"] = "line one\nline two=value",
            ["$dpapi-secret:Password"] = "protected:old",
            ["$dpapi-secret:RDGatewayPassword"] = "protected:gateway"
        }, ["PrivateKeyPath"]);
        var fields = ConnectionProtocolSettings.Read(original);
        var updated = fields.Apply(original, ProtocolKind.Rdp);
        Assert.That(updated!.Values, Is.EquivalentTo(original.Values));
        Assert.That(updated.InheritedProperties, Is.EqualTo(original.InheritedProperties));
        Assert.That(original.Values["RedirectDiskDrives"], Is.EqualTo("Custom"));
    }

    [TestCase(ProtocolKind.Ssh2, "PrivateKeyPath", "changed-key.ppk")]
    [TestCase(ProtocolKind.Rdp, "Domain", "TEST")]
    [TestCase(ProtocolKind.Vnc, "ViewOnly", "True")]
    public void StructuredFieldsApplyOnlyTheSelectedProtocol(ProtocolKind protocol, string name, string expected)
    {
        var fields = new ConnectionProtocolSettings
        {
            PrivateKeyPath = "changed-key.ppk", Domain = "TEST", ViewOnly = true,
            ShareClipboard = false, ShareLocalDrives = false, FitToWindow = false
        };
        var updated = fields.Apply(null, protocol)!;
        Assert.That(updated.Values[name], Is.EqualTo(expected));
        if (protocol != ProtocolKind.Ssh2)
            Assert.That(updated.Values.ContainsKey("PrivateKeyPath"), Is.False);
        if (protocol != ProtocolKind.Rdp)
            Assert.That(updated.Values.ContainsKey("RedirectClipboard"), Is.False);
        if (protocol != ProtocolKind.Vnc)
            Assert.That(updated.Values.ContainsKey("SmartSize"), Is.False);
    }

    [TestCase("new-key.ppk")]
    [TestCase("")]
    public void EditingAnInheritedKeyOverridesOnlyThatField(string path)
    {
        var inherited = new ConnectionNodeOptions(new Dictionary<string, string> { ["PrivateKeyPath"] = "parent-key.ppk" }, []);
        var raw = new ConnectionNodeOptions(new Dictionary<string, string> { ["PrivateKeyPath"] = "fallback.ppk" }, ["PrivateKeyPath", "Domain"]);
        var initial = ConnectionProtocolSettings.Read(inherited);
        var updated = (initial with { PrivateKeyPath = path }).Apply(raw, ProtocolKind.Ssh2, initial)!;
        Assert.That(updated.Values["PrivateKeyPath"], Is.EqualTo(path));
        Assert.That(updated.InheritedProperties, Is.EqualTo((string[])["Domain"]));
        Assert.That(raw.Values["PrivateKeyPath"], Is.EqualTo("fallback.ppk"));
    }

    [TestCase(null, false, "protected:old")]
    [TestCase("changed", false, "protected:changed")]
    [TestCase(null, true, "")]
    public void StructuredPasswordInputPreservesOtherSecrets(string? password, bool clear, string expected)
    {
        var original = new ConnectionNodeOptions(new Dictionary<string, string>
        {
            ["$dpapi-secret:Password"] = "protected:old",
            ["$dpapi-secret:RDGatewayPassword"] = "protected:gateway",
            ["SSHOptions"] = "a=b\nunchanged"
        }, ["Password", "Domain"]);
        var updated = new ConnectionOptionsEditor(new RecordingSecretStore())
            .BuildFromFields(Guid.NewGuid(), original, password, clear)!;
        Assert.That(updated.Values["$dpapi-secret:Password"], Is.EqualTo(expected));
        Assert.That(updated.Values["$dpapi-secret:RDGatewayPassword"], Is.EqualTo("protected:gateway"));
        Assert.That(updated.Values["SSHOptions"], Is.EqualTo("a=b\nunchanged"));
        Assert.That(updated.InheritedProperties.Contains("Password"), Is.EqualTo(!clear && password is null));
    }

    [TestCase(ProtocolKind.Ssh2)]
    [TestCase(ProtocolKind.Rdp)]
    [TestCase(ProtocolKind.Vnc)]
    public void DefaultBackfillIsIdempotentAndKeepsExistingSettings(ProtocolKind protocol)
    {
        var original = new ConnectionNodeOptions(new Dictionary<string, string>
        {
            ["PrivateKeyPath"] = "existing.ppk", ["RedirectClipboard"] = "False",
            ["RedirectDiskDrives"] = "Custom", ["ViewOnly"] = "True",
            ["$dpapi-secret:Password"] = "protected:old"
        }, ["PrivateKeyPath"]);
        var updated = ConnectionProtocolSettings.AddMissingDefaults(original, protocol);
        foreach (var pair in original.Values)
            Assert.That(updated.Values[pair.Key], Is.EqualTo(pair.Value));
        Assert.That(updated.InheritedProperties, Is.EqualTo(original.InheritedProperties));
        Assert.That(ConnectionProtocolSettings.AddMissingDefaults(updated, protocol).Values, Is.EquivalentTo(updated.Values));
    }

    [TestCase(ProtocolKind.Ssh2)]
    [TestCase(ProtocolKind.Rdp)]
    [TestCase(ProtocolKind.Vnc)]
    public void BackfillDoesNotShadowFolderSettings(ProtocolKind protocol)
    {
        var inherited = new ConnectionNodeOptions(new Dictionary<string, string>
        {
            ["PrivateKeyPath"] = "parent.ppk", ["Domain"] = "PARENT", ["RedirectClipboard"] = "False",
            ["RedirectDiskDrives"] = "None", ["ViewOnly"] = "True", ["SmartSize"] = "False"
        }, []);
        Assert.That(ConnectionProtocolSettings.AddMissingDefaults(null, protocol, inherited).Values, Is.Empty);
    }

    [TestCase(ProtocolKind.Ssh2, "PrivateKeyPath", "")]
    [TestCase(ProtocolKind.Rdp, "RedirectClipboard", "True")]
    [TestCase(ProtocolKind.Rdp, "RedirectDiskDrives", "Local")]
    [TestCase(ProtocolKind.Vnc, "ViewOnly", "False")]
    [TestCase(ProtocolKind.Vnc, "SmartSize", "True")]
    public void NewConnectionsUseRuntimeDefaults(ProtocolKind protocol, string name, string expected)
    {
        Assert.That(ConnectionProtocolSettings.AddMissingDefaults(null, protocol).Values[name], Is.EqualTo(expected));
        Assert.That(new ConnectionProtocolSettings().Apply(null, protocol), Is.Null);
    }

    [TestCase(22, ProtocolKind.Ssh2, ProtocolKind.Rdp, 3389)]
    [TestCase(3389, ProtocolKind.Rdp, ProtocolKind.Vnc, 5900)]
    [TestCase(22220, ProtocolKind.Ssh2, ProtocolKind.Rdp, 22220)]
    public void ProtocolSelectionChangesOnlyDefaultPorts(double port, ProtocolKind previous, ProtocolKind selected, double expected)
    {
        Assert.That(ConnectionProtocolSettings.ChangePort(port, previous, selected), Is.EqualTo(expected));
    }

    private sealed class RecordingSecretStore : IStringSecretStore
    {
        public string Protect(string plaintext, string purpose) => "protected:" + plaintext;

        public string Unprotect(string protectedValue, string purpose) => protectedValue["protected:".Length..];
    }
}
