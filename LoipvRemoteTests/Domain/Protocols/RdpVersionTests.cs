using NUnit.Framework;

namespace LoipvRemoteTests.Domain.Protocols;

public class RdpVersionTests
{
    [TestCase(RdpVersion.Rdc6, 0)]
    [TestCase(RdpVersion.Rdc11, 5)]
    [TestCase(RdpVersion.Highest, 1000)]
    public void PersistedValues_AreStable(RdpVersion version, int expectedValue)
    {
        Assert.That((int)version, Is.EqualTo(expectedValue));
    }
}
