using System.Diagnostics;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class ProductAssemblyVersionTests
{
    [Test]
    public void EveryShippedLoipvRemoteAssemblyUsesTheApplicationFileVersion()
    {
        string testDirectory = TestContext.CurrentContext.TestDirectory;
        string application = Path.Combine(testDirectory, "LoipvRemote.dll");
        string expectedVersion = FileVersionInfo.GetVersionInfo(application).FileVersion!;
        string[] mismatches = Directory.GetFiles(testDirectory, "LoipvRemote*.dll")
            .Where(path => !Path.GetFileName(path).Contains("Tests", StringComparison.Ordinal))
            .Select(path => new
            {
                Name = Path.GetFileName(path),
                Version = FileVersionInfo.GetVersionInfo(path).FileVersion
            })
            .Where(assembly => assembly.Version != expectedVersion)
            .Select(assembly => $"{assembly.Name}: {assembly.Version}")
            .ToArray();

        Assert.That(mismatches, Is.Empty,
            $"All shipped assemblies must use {expectedVersion} so MSI upgrades overwrite changed files.");
    }
}
