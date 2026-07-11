using System;

namespace LoipvRemote.Messages
{
    public interface IMessage
    {
        MessageClass Class { get; set; }

        string Text { get; set; }

        DateTime Date { get; set; }

        bool OnlyLog { get; set; }
    }
}