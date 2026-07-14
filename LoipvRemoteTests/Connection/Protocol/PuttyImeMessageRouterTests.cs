using LoipvRemote.Infrastructure.Windows.Interop;
using LoipvRemote.Protocols.Putty;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol
{
    [TestFixture]
    public class PuttyImeMessageRouterTests
    {
        [TestCase(NativeMethods.WM_INPUTLANGCHANGE)]
        [TestCase(NativeMethods.WM_IME_STARTCOMPOSITION)]
        [TestCase(NativeMethods.WM_IME_ENDCOMPOSITION)]
        [TestCase(NativeMethods.WM_IME_COMPOSITION)]
        [TestCase(NativeMethods.WM_IME_CHAR)]
        [TestCase(NativeMethods.WM_IME_KEYDOWN)]
        [TestCase(NativeMethods.WM_IME_KEYUP)]
        public void RecognizesImeMessages(int message)
        {
            Assert.That(PuttyImeMessageRouter.ShouldForward(message), Is.True);
        }

        [TestCase(NativeMethods.WM_KEYDOWN)]
        [TestCase(NativeMethods.WM_CHAR)]
        [TestCase(NativeMethods.WM_MOUSEMOVE)]
        public void DoesNotForwardRegularMessages(int message)
        {
            Assert.That(PuttyImeMessageRouter.ShouldForward(message), Is.False);
        }
    }
}
