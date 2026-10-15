using LoipvRemote.Domain.Connections;
using LoipvRemote.WinUI.Sessions;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Sessions;

public sealed class SessionPresentationPolicyTests
{
    [Test]
    public void TabSelectionActivatesTheSelectedNativeSession()
    {
        bool shouldActivate = SessionPresentationPolicy.ShouldActivateNativeSession(
            SessionPresentationTrigger.TabSelection);

        Assert.That(shouldActivate, Is.True);
    }

    [Test]
    public void ConnectionCompletionDoesNotActivateTheNativeSessionTwice()
    {
        bool shouldActivate = SessionPresentationPolicy.ShouldActivateNativeSession(
            SessionPresentationTrigger.ConnectionCompleted);

        Assert.That(shouldActivate, Is.False);
    }

    [TestCase(RemoteSessionTabState.Created, true)]
    [TestCase(RemoteSessionTabState.Connecting, true)]
    [TestCase(RemoteSessionTabState.Connected, false)]
    [TestCase(RemoteSessionTabState.Faulted, true)]
    [TestCase(RemoteSessionTabState.Closed, true)]
    public void TabsWithoutAConnectedSessionHideThePreviousNativeSurface(
        RemoteSessionTabState state,
        bool expected)
    {
        Assert.That(SessionPresentationPolicy.ShouldDeactivateNativeSurface(state), Is.EqualTo(expected));
    }

    [Test]
    public void BackgroundConnectionCompletionRestoresTheCurrentlySelectedSession()
    {
        var completedSession = new object();
        var selectedSession = new object();

        Assert.Multiple(() =>
        {
            Assert.That(
                SessionPresentationPolicy.ShouldRestoreSelectedSession(completedSession, selectedSession),
                Is.True);
            Assert.That(
                SessionPresentationPolicy.ShouldRestoreSelectedSession(selectedSession, selectedSession),
                Is.False);
        });
    }

    [Test]
    public void ConnectionFailureStatusKeepsTheActionableReasonVisible()
    {
        string status = SessionPresentationPolicy.FormatConnectionFailure("Drive redirection is unavailable.");

        Assert.That(status, Is.EqualTo("Connection failed: Drive redirection is unavailable."));
    }

    [TestCase("", "server.example", ProtocolKind.Ssh2, 22, "Enter a connection name.")]
    [TestCase("server", " ", ProtocolKind.Ssh2, 22, "Enter a host name or IP address.")]
    [TestCase("server", "server.example", null, 22, "Select a supported protocol.")]
    [TestCase("server", "server.example", ProtocolKind.Ssh2, 0, "Enter a port between 1 and 65535.")]
    [TestCase("server", "server.example", ProtocolKind.Ssh2, 65536, "Enter a port between 1 and 65535.")]
    [TestCase("server", "server.example", ProtocolKind.Ssh2, 22, null)]
    public void ConnectionDialogValidationExplainsTheFirstFieldToFix(
        string name,
        string host,
        ProtocolKind? protocol,
        double port,
        string? expected)
    {
        Assert.That(ConnectionDialogValidation.GetError(name, host, protocol, port), Is.EqualTo(expected));
    }

    [TestCase("", ProtocolKind.Ssh2, 22, "Enter a host name or IP address.")]
    [TestCase("server.example", null, 22, "Select a supported protocol.")]
    [TestCase("server.example", ProtocolKind.Ssh2, double.NaN, "Enter a port between 1 and 65535.")]
    [TestCase("server.example", ProtocolKind.Ssh2, 3389, null)]
    public void QuickConnectValidationKeepsInvalidInputInTheDialog(
        string host,
        ProtocolKind? protocol,
        double port,
        string? expected)
    {
        Assert.That(ConnectionDialogValidation.GetQuickConnectError(host, protocol, port), Is.EqualTo(expected));
    }

    [TestCase("", "Enter a folder name.")]
    [TestCase("   ", "Enter a folder name.")]
    [TestCase("Production", null)]
    public void FolderValidationRequiresAVisibleName(string name, string? expected)
    {
        Assert.That(ConnectionDialogValidation.GetFolderError(name), Is.EqualTo(expected));
    }

    [TestCase("", "secret", "Enter a credential name.")]
    [TestCase("Production", "", "Enter a password.")]
    [TestCase("Production", "secret", null)]
    public void CredentialValidationExplainsMissingRequiredFields(string name, string password, string? expected)
    {
        Assert.That(ConnectionDialogValidation.GetCredentialError(name, password), Is.EqualTo(expected));
    }

    [Test]
    public void PortableExportWarningStatesThatCredentialsArePlaintext()
    {
        string warning = PortableExportPolicy.GetWarning(57);

        Assert.Multiple(() =>
        {
            Assert.That(warning, Does.Contain("57 connections"));
            Assert.That(warning, Does.Contain("plaintext"));
            Assert.That(warning, Does.Contain("secure location"));
        });
    }
}
