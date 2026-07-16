using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace LoipvRemote.Messages;

/// <summary>Collects application messages and publishes UI-bindable changes.</summary>
public sealed class MessageCollector : INotifyCollectionChanged
{
    private readonly List<IMessage> _messageList = [];

    public IEnumerable<IMessage> Messages => _messageList;

    public void AddMessage(MessageClass messageClass, string messageText, bool onlyLog = false) =>
        AddMessage(new Message(messageClass, messageText, onlyLog));

    public void AddMessage(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        AddMessages([message]);
    }

    public void AddMessages(IEnumerable<IMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        List<IMessage> newMessages = [];
        foreach (IMessage message in messages)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (_messageList.Contains(message))
                continue;

            _messageList.Add(message);
            newMessages.Add(message);
        }

        if (newMessages.Count > 0)
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add, (IList)newMessages));
    }

    public void AddExceptionMessage(
        string message,
        Exception exception,
        MessageClass messageClass = MessageClass.ErrorMsg,
        bool logOnly = true) =>
        AddMessage(messageClass, message + Environment.NewLine + exception, logOnly);

    public void AddExceptionStackTrace(
        string message,
        Exception exception,
        MessageClass messageClass = MessageClass.ErrorMsg,
        bool logOnly = true) =>
        AddMessage(messageClass, message + Environment.NewLine + exception.Message + Environment.NewLine + exception.StackTrace, logOnly);

    public void ClearMessages() => _messageList.Clear();

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
}
