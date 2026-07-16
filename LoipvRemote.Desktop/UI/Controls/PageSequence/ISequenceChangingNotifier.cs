using System;

namespace LoipvRemote.UI.Controls.PageSequence
{
    public interface ISequenceChangingNotifier
    {
        event EventHandler NextRequested;
        event EventHandler Previous;
        event SequencedPageReplcementRequestHandler PageReplacementRequested;
    }
}
