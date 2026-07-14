using System.Text;
using LoipvRemote.Protocols.Rdp;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

public class AzureLoadBalanceInfoEncoderTests
{
    [Test]
    public void EncodesOddLengthInputWithRequiredPaddingAndLineEnding()
    {
        var encoded = new AzureLoadBalanceInfoEncoder().Encode("abc");

        var payloadSeenByTheActiveXControl = Encoding.UTF8.GetString(Encoding.Unicode.GetBytes(encoded));

        Assert.That(payloadSeenByTheActiveXControl, Is.EqualTo("abc \r\n"));
    }
}
