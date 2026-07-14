using LoipvRemote.Protocols.ExternalApps;
using LoipvRemote.Protocols.Abstractions;
using NUnit.Framework;
using System.Collections.Generic;

namespace LoipvRemoteTests.Protocols.ExternalApps;

[TestFixture]
public class ExternalApplicationCommandLineTests
{
    [Test]
    public void SplitArguments_PreservesQuotedValuesAndCollapsesWhitespace()
    {
        IReadOnlyList<string> arguments = ExternalApplicationCommandLine.SplitArguments(
            "--host \"server name\"   --user admin");

        Assert.That(arguments, Is.EqualTo(new[] { "--host", "server name", "--user", "admin" }));
    }

    [Test]
    public void SplitArguments_PreservesEscapedQuotesAndBackslashes()
    {
        IReadOnlyList<string> arguments = ExternalApplicationCommandLine.SplitArguments(
            "--title \"say \\\"hello\\\"\" --path C:\\Tools\\");

        Assert.That(arguments, Is.EqualTo(new[] { "--title", "say \"hello\"", "--path", "C:\\Tools\\" }));
    }

    [Test]
    public void SplitArguments_EmptyInputReturnsNoArguments()
    {
        Assert.That(ExternalApplicationCommandLine.SplitArguments("   "), Is.Empty);
    }
}
