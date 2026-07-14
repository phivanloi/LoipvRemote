using System.Xml.Linq;
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

    private static readonly string[] ForbiddenDirectReferences =
    [
        "ConsoleControl",
        "System.Management",
        "VncSharp",
        "Microsoft.Web.WebView2",
        "Interop.MSTSCLib",
        "AxInterop.MSTSCLib"
    ];

    [Test]
    public void Monolith_DoesNotOwnProtocolRuntimeOrWindowsInterop()
    {
        string repositoryRoot = FindRepositoryRoot();
        string monolithRoot = Path.Combine(repositoryRoot, "LoipvRemote");
        List<string> violations = [];

        foreach (string file in Directory.EnumerateFiles(monolithRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !IsBuildArtifact(path)))
        {
            string source = File.ReadAllText(file);
            foreach (string marker in ForbiddenSourceMarkers.Where(source.Contains))
                violations.Add($"{Path.GetRelativePath(repositoryRoot, file)} contains {marker}");
        }

        XDocument project = XDocument.Load(Path.Combine(monolithRoot, "LoipvRemote.csproj"));
        foreach (XElement reference in project.Descendants()
                     .Where(element => element.Name.LocalName is "PackageReference" or "Reference"))
        {
            string? include = reference.Attribute("Include")?.Value;
            if (include is not null && ForbiddenDirectReferences.Contains(include, StringComparer.OrdinalIgnoreCase))
                violations.Add($"LoipvRemote.csproj directly references {include}");
        }

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }

    [Test]
    public void DesktopShellRoutesProtocolInputThroughProtocolBoundary()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] shellFiles =
        [
            Path.Combine(repositoryRoot, "LoipvRemote", "Connection", "InterfaceControl.cs"),
            Path.Combine(repositoryRoot, "LoipvRemote", "UI", "Forms", "frmMain.cs")
        ];

        List<string> violations = [];
        foreach (string file in shellFiles)
        {
            string source = File.ReadAllText(file);
            if (source.Contains("PuttyImeMessageRouter", StringComparison.Ordinal) ||
                source.Contains("NativeMethods.SendMessage(putty", StringComparison.Ordinal) ||
                source.Contains("putty.PuttyHandle", StringComparison.Ordinal))
            {
                violations.Add($"{Path.GetRelativePath(repositoryRoot, file)} reaches PuTTY input internals directly.");
            }
        }

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
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
