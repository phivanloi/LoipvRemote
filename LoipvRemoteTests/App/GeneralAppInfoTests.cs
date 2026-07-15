using LoipvRemote.App.Info;
using NUnit.Framework;

namespace LoipvRemoteTests.App;

public sealed class GeneralAppInfoTests
{
    [TestCase("2.0.31.0+89b07628958881ed574cc1266912e", "2.0.31.0")]
    [TestCase("2.0.31.0 Build 42", "2.0.31.0")]
    [TestCase("2.0.31", "2.0.31.0")]
    [TestCase("invalid", "0.0.0.0")]
    [TestCase(null, "0.0.0.0")]
    public void NormalizesProductVersionToNumericValue(string? input, string expected)
    {
        Assert.That(GeneralAppInfo.NormalizeProductVersion(input), Is.EqualTo(expected));
    }
}
