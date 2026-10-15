using LoipvRemote.WinUI.Sessions;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Sessions;

public sealed class SessionTabCloseGateTests
{
    [Test]
    public void GateRejectsOverlappingCloseRequestsAndAllowsTheNextGestureAfterExit()
    {
        var gate = new SessionTabCloseGate();

        Assert.That(gate.TryEnter(), Is.True);
        Assert.That(gate.TryEnter(), Is.False);

        gate.Exit();

        Assert.That(gate.TryEnter(), Is.True);
    }
}
