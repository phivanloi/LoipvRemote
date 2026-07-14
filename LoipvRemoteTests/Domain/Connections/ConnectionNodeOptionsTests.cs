using LoipvRemote.Domain.Connections;
using NUnit.Framework;

namespace LoipvRemoteTests.Domain.Connections;

public sealed class ConnectionNodeOptionsTests
{
    [TestCase("Password")]
    [TestCase("RDGatewayPassword")]
    [TestCase("RDGatewayAccessToken")]
    [TestCase("VNCProxyPassword")]
    public void RejectsSecretValuesStoredAsOptions(string optionName)
    {
        var options = new ConnectionNodeOptions(
            new Dictionary<string, string> { [optionName] = "secret" },
            []);

        Assert.That(options.Validate, Throws.ArgumentException);
    }
}
