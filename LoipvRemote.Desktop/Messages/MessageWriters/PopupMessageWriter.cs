using System;
using System.Windows.Forms;
using LoipvRemote.Resources.Language;

namespace LoipvRemote.Messages.MessageWriters
{
    public class PopupMessageWriter : IMessageWriter
    {
        public void Write(IMessage message)
        {
            switch (message.MessageClass)
            {
                case MessageClass.DebugMsg:
                    MessageBox.Show(message.Text, FormatText(Language.TitleInformation, message.Timestamp),
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                case MessageClass.InformationMsg:
                    MessageBox.Show(message.Text, FormatText(Language.TitleInformation, message.Timestamp),
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                case MessageClass.WarningMsg:
                    MessageBox.Show(message.Text, FormatText(Language.TitleWarning, message.Timestamp),
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
                case MessageClass.ErrorMsg:
                    MessageBox.Show(message.Text, FormatText(Language.TitleError, message.Timestamp),
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported message class: {message.MessageClass}.");
            }
        }
    }
}
