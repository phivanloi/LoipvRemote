using LoipvRemote.WinUI.Sessions;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Sessions;

public sealed class LocalPowerShellRuntimeLocatorTests
{
    [Test]
    public void BundledRuntimeAlwaysHasHighestPriority()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string bundled = Path.Combine(root, "app", "Runtime", "PowerShell", "pwsh.exe");
        string installed = Path.Combine(root, "program-files", "PowerShell", "7", "pwsh.exe");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(bundled)!);
            Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
            File.WriteAllText(bundled, string.Empty);
            File.WriteAllText(installed, string.Empty);

            var locator = new LocalPowerShellRuntimeLocator(
                Path.Combine(root, "app"),
                Path.Combine(root, "program-files"),
                null);

            Assert.That(locator.Resolve(), Is.EqualTo(Path.GetFullPath(bundled)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void InstalledRuntimeSupportsDeveloperBuildsWithoutBundledPayload()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string installed = Path.Combine(root, "program-files", "PowerShell", "7", "pwsh.exe");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
            File.WriteAllText(installed, string.Empty);

            var locator = new LocalPowerShellRuntimeLocator(
                Path.Combine(root, "app"),
                Path.Combine(root, "program-files"),
                null);

            Assert.That(locator.Resolve(), Is.EqualTo(Path.GetFullPath(installed)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void MissingRuntimeProducesReinstallMessage()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var locator = new LocalPowerShellRuntimeLocator(root, null, null);

        var exception = Assert.Throws<FileNotFoundException>(() => locator.Resolve());

        Assert.That(exception!.Message, Does.Contain("Reinstall Loipv Remote"));
    }
}
