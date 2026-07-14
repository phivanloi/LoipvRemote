using LoipvRemote.Protocols.Rdp;
using NUnit.Framework;

namespace LoipvRemoteTests.Protocols.Rdp;

public class RdpErrorResourceKeysTests
{
    [TestCase(1, "RdpErrorCode1")]
    [TestCase(2, "RdpErrorOutOfMemory")]
    [TestCase(7, "RdpErrorConnection")]
    [TestCase(100, "RdpErrorWinsock")]
    [TestCase(0, "RdpErrorUnknown")]
    [TestCase(999, "RdpErrorUnknown")]
    public void GetErrorResourceKey_ReturnsStableKey(int errorCode, string expectedKey)
    {
        Assert.That(RdpErrorResourceKeys.GetErrorResourceKey(errorCode), Is.EqualTo(expectedKey));
    }
}
