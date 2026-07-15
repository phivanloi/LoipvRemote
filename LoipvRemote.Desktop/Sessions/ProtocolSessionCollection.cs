using System.Collections;
using System.Collections.Specialized;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Desktop.Sessions;

/// <summary>Tracks the protocol sessions currently hosted by a connection tab.</summary>
public sealed class ProtocolSessionCollection : IList<IProtocolSession>, IReadOnlyList<IProtocolSession>, INotifyCollectionChanged
{
    private readonly List<IProtocolSession> _sessions = [];

    public IProtocolSession this[int index]
    {
        get => _sessions[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            IProtocolSession previous = _sessions[index];
            _sessions[index] = value;
            CollectionChanged?.Invoke(this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, previous, index));
        }
    }

    /// <summary>Compatibility indexer for callers that search by session instance.</summary>
    public IProtocolSession? this[object index] => index switch
    {
        IProtocolSession session when _sessions.Contains(session) => session,
        int integer when integer >= 0 && integer < _sessions.Count => _sessions[integer],
        _ => null
    };

    public int Count => _sessions.Count;
    public bool IsReadOnly => false;

    public void Add(IProtocolSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions.Add(session);
        RaiseAdded(new[] { session });
    }

    public void AddRange(IProtocolSession[] sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        foreach (IProtocolSession session in sessions)
        {
            ArgumentNullException.ThrowIfNull(session);
            _sessions.Add(session);
        }

        if (sessions.Length > 0)
            RaiseAdded(sessions);
    }

    public bool Remove(IProtocolSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        int index = _sessions.IndexOf(session);
        if (index < 0)
            return false;

        _sessions.RemoveAt(index);
        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, session, index));
        return true;
    }

    public void Clear()
    {
        if (_sessions.Count == 0)
            return;

        _sessions.Clear();
        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public bool Contains(IProtocolSession session) => _sessions.Contains(session);
    public void CopyTo(IProtocolSession[] array, int arrayIndex) => _sessions.CopyTo(array, arrayIndex);
    public int IndexOf(IProtocolSession session) => _sessions.IndexOf(session);

    public void Insert(int index, IProtocolSession item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _sessions.Insert(index, item);
        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    public void RemoveAt(int index)
    {
        IProtocolSession session = _sessions[index];
        _sessions.RemoveAt(index);
        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, session, index));
    }

    public IEnumerator<IProtocolSession> GetEnumerator() => _sessions.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    private void RaiseAdded(IList sessions) => CollectionChanged?.Invoke(this,
        new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, sessions));
}
