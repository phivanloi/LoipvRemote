using System;
using System.Windows.Forms;
using LoipvRemote.Messages;

namespace LoipvRemote.UI
{
    public class NotificationMessageListViewItem : ListViewItem
    {
        public NotificationMessageListViewItem(IMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            ImageIndex = Convert.ToInt32(message.MessageClass, CultureInfo.InvariantCulture);
            Text = message.Text.Replace(Environment.NewLine, "  ");
            Tag = message;
        }
    }
}
