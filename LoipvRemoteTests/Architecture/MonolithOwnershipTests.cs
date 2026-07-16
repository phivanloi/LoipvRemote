using LoipvRemote.UI.Adapters;
using NUnit.Framework;

namespace LoipvRemoteTests.Architecture;

[TestFixture]
public sealed class MonolithOwnershipTests
{
    private static readonly string[] ForbiddenSourceMarkers =
    [
        "[DllImport(",
        "[LibraryImport(",
        "ConsoleControl.ConsoleControl",
        "using MSTSCLib;",
        "using AxMSTSCLib;",
        "using VncSharp;",
        "using Microsoft.Web.WebView2;",
        "ProtectedData.Unprotect(",
        "ProtectedData.Protect(",
        "Marshal."
    ];

    [Test]
    public void Monolith_DoesNotOwnProtocolRuntimeOrWindowsInterop()
    {
        string repositoryRoot = FindRepositoryRoot();
        string monolithRoot = Path.Combine(repositoryRoot, "LoipvRemote.Desktop");
        List<string> violations = [];

        foreach (string file in Directory.EnumerateFiles(monolithRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !IsBuildArtifact(path)))
        {
            string source = File.ReadAllText(file);
            foreach (string marker in ForbiddenSourceMarkers.Where(source.Contains))
                violations.Add($"{Path.GetRelativePath(repositoryRoot, file)} contains {marker}");
            if (source.Contains("NativeMethods.", StringComparison.Ordinal))
                violations.Add($"{Path.GetRelativePath(repositoryRoot, file)} reaches the Win32 P/Invoke type directly.");
        }

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }

    [Test]
    public void ProtocolLifecycleHostAndSessionCollectionLiveOutsideTheExecutableRoot()
    {
        string repositoryRoot = FindRepositoryRoot();
        string rootProtocolDirectory = Path.Combine(repositoryRoot, "LoipvRemote.Desktop", "Connection", "Protocol");

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(rootProtocolDirectory, "ProtocolBase.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(rootProtocolDirectory, "ProtocolList.cs")), Is.False);
            Assert.That(
                File.Exists(Path.Combine(repositoryRoot, "LoipvRemote.Desktop", "Sessions", "DesktopSessionHost.cs")),
                Is.True);
            Assert.That(
                File.Exists(Path.Combine(repositoryRoot, "LoipvRemote.Desktop", "Sessions", "ProtocolSessionCollection.cs")),
                Is.True);
            Assert.That(
                File.Exists(Path.Combine(repositoryRoot, "LoipvRemote.Desktop", "UI", "Adapters", "ProtocolSessionBridge.cs")),
                Is.True);
            Assert.That(
                Directory.Exists(rootProtocolDirectory),
                Is.False,
                "The executable root must not retain an empty protocol namespace directory.");
        });
    }

    [Test]
    public void DesktopShellRoutesProtocolInputThroughProtocolBoundary()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] shellFiles =
        [
            Path.Combine(repositoryRoot, "LoipvRemote.Desktop", "Connection", "InterfaceControl.cs"),
            Path.Combine(repositoryRoot, "LoipvRemote.Desktop", "UI", "Forms", "frmMain.cs"),
            Path.Combine(repositoryRoot, "LoipvRemote.Desktop", "UI", "Controls", "MultiSshToolStrip.cs")
        ];

        List<string> violations = [];
        foreach (string file in shellFiles)
        {
            string source = File.ReadAllText(file);
            if (source.Contains("PuttyInputMessageRouter", StringComparison.Ordinal) ||
                source.Contains("NativeMethods.SendMessage(putty", StringComparison.Ordinal) ||
                source.Contains("putty.PuttyHandle", StringComparison.Ordinal) ||
                source.Contains("PostMessage(proc.PuttyHandle", StringComparison.Ordinal))
            {
                violations.Add($"{Path.GetRelativePath(repositoryRoot, file)} reaches PuTTY input internals directly.");
            }
        }

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }

    [Test]
    public void ConnectionInitiatorUsesDomainFactoryForDirectSessions()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "LoipvRemote.Desktop",
            "Connection",
            "ConnectionInitiator.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("IProtocolFactory protocolFactory"));
            Assert.That(source, Does.Contain("_protocolFactory.Create(definition)"));
            Assert.That(source, Does.Contain("new ProtocolSessionBridge(definition, domainSession)"));
        });
    }

    [Test]
    public void ConnectionInitiatorDoesNotOwnConnectionStoreService()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "LoipvRemote.Desktop",
            "Connection",
            "ConnectionInitiator.cs"));

        Assert.That(source, Does.Not.Contain("ConnectionsService"));
        Assert.That(source, Does.Contain("Func<string, ConnectionInfo?> connectionLookup"));
    }

    private static bool IsBuildArtifact(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoipvRemote.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate LoipvRemote.slnx.");
    }
}
