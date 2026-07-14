using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

[TestFixture]
public class EmbeddedWindowActivationPolicyTests
{
    [TestCase(false, true, true, true)]
    [TestCase(true, true, true, false)]
    [TestCase(false, false, true, false)]
    [TestCase(false, true, false, false)]
    public void RequestsFocusOnlyForTheLiveVisibleHost(
        bool hostIsDisposed,
        bool hostHasHandle,
        bool hostIsVisible,
        bool expected)
    {
        bool result = EmbeddedWindowActivationPolicy.ShouldRequestFocus(
            hostIsDisposed,
            hostHasHandle,
            hostIsVisible);

        Assert.That(result, Is.EqualTo(expected));
    }
}
