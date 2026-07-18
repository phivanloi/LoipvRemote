namespace LoipvRemote.WinUI.Sessions;

/// <summary>Prevents one input gesture from starting overlapping tab closes.</summary>
internal sealed class SessionTabCloseGate
{
    private int _isClosing;

    public bool TryEnter() => Interlocked.CompareExchange(ref _isClosing, 1, 0) == 0;

    public void Exit() => Volatile.Write(ref _isClosing, 0);
}
