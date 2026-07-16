using System.Diagnostics;

namespace LoipvRemote.Messages.MessageWriters
{
    public class DebugConsoleMessageWriter : IMessageWriter
    {
        public void Write(IMessage message)
        {
            string textToPrint = $"{message.MessageClass}: {message.Text}";
            Debug.Print(textToPrint);
        }
    }
}
