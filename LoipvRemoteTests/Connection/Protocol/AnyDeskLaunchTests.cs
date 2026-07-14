using LoipvRemote.Protocols.Abstractions;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

[TestFixture]
public sealed class AnyDeskLaunchTests
{
    [TestCase("123456789")]
    [TestCase("support-team@example.ad")]
    public void AcceptsSupportedIdentifiers(string identifier) =>
        Assert.That(AnyDeskLaunch.IsValidIdentifier(identifier), Is.True);

    [TestCase("")]
    [TestCase("host & calc")]
    [TestCase("host\" --with-password")]
    public void RejectsIdentifiersThatCanChangeTheCommandLine(string identifier) =>
        Assert.That(AnyDeskLaunch.IsValidIdentifier(identifier), Is.False);

    [Test]
    public void AddsPasswordFlagWithoutAddingThePassword()
    {
        IReadOnlyList<string> arguments = AnyDeskLaunch.BuildArguments("123456789", hasPassword: true);

        Assert.That(arguments, Is.EqualTo(new[] { "123456789", "--with-password", "--plain" }));
    }
}
