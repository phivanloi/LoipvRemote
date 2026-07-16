using System;

namespace LoipvRemote.Messages;

public interface IMessage
{
    MessageClass MessageClass { get; set; }
    string Text { get; set; }
    DateTime Timestamp { get; set; }
    bool OnlyLog { get; set; }
}
