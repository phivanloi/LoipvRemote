using System;

namespace LoipvRemote.Messages;

public sealed class Message(MessageClass messageClass, string messageText, bool onlyLog = false) : IMessage
{
    public MessageClass MessageClass { get; set; } = messageClass;
    public string Text { get; set; } = messageText;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool OnlyLog { get; set; } = onlyLog;
}
