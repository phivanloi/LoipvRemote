using LoipvRemote.Protocols.Abstractions;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

[TestFixture]
public sealed class ProtocolAnyDeskTests
{
    [TestCase("123456789")]
    [TestCase("myalias@ad")]
    [TestCase("my-alias@ad")]
    [TestCase("my_alias@ad")]
    [TestCase("alias.name@ad")]
    public void AcceptsSupportedIdentifiers(string identifier) =>
        Assert.That(AnyDeskLaunch.IsValidIdentifier(identifier), Is.True);

    [TestCase("123456789; calc.exe")]
    [TestCase("123456789 & calc.exe")]
    [TestCase("123456789 | calc.exe")]
    [TestCase("123456789 > output.txt")]
    [TestCase("123456789`calc")]
    [TestCase("123456789$var")]
    [TestCase("123456789(calc)")]
    [TestCase("123456789\ncalc")]
    [TestCase("123456789\rcalc")]
    [TestCase("123456789\"calc\"")]
    [TestCase("123456789'calc'")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void RejectsUnsupportedIdentifiers(string? identifier) =>
        Assert.That(AnyDeskLaunch.IsValidIdentifier(identifier), Is.False);
}
