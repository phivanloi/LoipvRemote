using LoipvRemote.Infrastructure.Windows.Interop;
using LoipvRemote.Protocols.Putty;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

[TestFixture]
public sealed class PuttyInputMessageRouterTests
{
    [TestCase(NativeMethods.WM_INPUTLANGCHANGE)]
    [TestCase(NativeMethods.WM_KEYDOWN)]
    [TestCase(NativeMethods.WM_KEYUP)]
    [TestCase(NativeMethods.WM_CHAR)]
    [TestCase(NativeMethods.WM_DEADCHAR)]
    [TestCase(NativeMethods.WM_SYSKEYDOWN)]
    [TestCase(NativeMethods.WM_SYSKEYUP)]
    [TestCase(NativeMethods.WM_SYSCHAR)]
    [TestCase(NativeMethods.WM_SYSDEADCHAR)]
    [TestCase(NativeMethods.WM_IME_STARTCOMPOSITION)]
    [TestCase(NativeMethods.WM_IME_ENDCOMPOSITION)]
    [TestCase(NativeMethods.WM_IME_COMPOSITION)]
    [TestCase(NativeMethods.WM_IME_CHAR)]
    [TestCase(NativeMethods.WM_IME_KEYDOWN)]
    [TestCase(NativeMethods.WM_IME_KEYUP)]
    public void RecognizesKeyboardAndImeMessages(int message)
    {
        Assert.That(PuttyInputMessageRouter.ShouldForward(message), Is.True);
    }

    [TestCase(NativeMethods.WM_MOUSEMOVE)]
    [TestCase(NativeMethods.WM_LBUTTONDOWN)]
    [TestCase(NativeMethods.WM_COMMAND)]
    public void DoesNotForwardNonInputMessages(int message)
    {
        Assert.That(PuttyInputMessageRouter.ShouldForward(message), Is.False);
    }
}
