using LoipvRemote.Desktop.Sessions;
using NUnit.Framework;

namespace LoipvRemoteTests.Desktop.Sessions;

[TestFixture]
public sealed class ProtocolFocusRetryPolicyTests
{
    [TestCase(0, true, true)]
    [TestCase(1, true, true)]
    [TestCase(4, true, false)]
    [TestCase(0, false, false)]
    public void ShouldAttempt_StopsAfterBoundedRetries(int attempt, bool active, bool expected)
    {
        Assert.That(ProtocolFocusRetryPolicy.ShouldAttempt(attempt, active), Is.EqualTo(expected));
    }
}
