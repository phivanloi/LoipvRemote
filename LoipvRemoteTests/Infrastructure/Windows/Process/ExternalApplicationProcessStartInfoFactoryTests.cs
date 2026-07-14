using LoipvRemote.Domain.Connections;
using LoipvRemote.Infrastructure.Windows.ProcessManagement;
using NUnit.Framework;

namespace LoipvRemoteTests.Infrastructure.Windows.ProcessManagement;

[TestFixture]
public class ExternalApplicationProcessStartInfoFactoryTests
{
    [Test]
    public void NonElevatedLaunchUsesArgumentList()
    {
        var definition = CreateDefinition(arguments: "--host \"server name\" --port 22");

        var startInfo = ExternalApplicationProcessStartInfoFactory.Create(definition);

        Assert.Multiple(() =>
        {
            Assert.That(startInfo.UseShellExecute, Is.False);
            Assert.That(startInfo.ArgumentList, Is.EqualTo(new[] { "--host", "server name", "--port", "22" }));
            Assert.That(startInfo.Arguments, Is.Empty);
        });
    }

    [Test]
    public void ElevatedLaunchUsesRunAsVerb()
    {
        var definition = CreateDefinition(arguments: "--host example") with { RunElevated = true, EmbedWindow = false };

        var startInfo = ExternalApplicationProcessStartInfoFactory.Create(definition);

        Assert.Multiple(() =>
        {
            Assert.That(startInfo.UseShellExecute, Is.True);
            Assert.That(startInfo.Verb, Is.EqualTo("runas"));
            Assert.That(startInfo.Arguments, Is.EqualTo("--host example"));
        });
    }

    [Test]
    public void RejectsLineBreakInExecutablePath()
    {
        var definition = CreateDefinition(arguments: string.Empty) with { ExecutablePath = "tool.exe\r\nother.exe" };

        Assert.That(
            () => ExternalApplicationProcessStartInfoFactory.Create(definition),
            Throws.ArgumentException);
    }

    private static ExternalApplicationDefinition CreateDefinition(string arguments) => new(
        "Tool", "tool.exe", arguments, string.Empty,
        RunElevated: false, EmbedWindow: true, WaitForExit: false);
}
