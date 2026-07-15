using LoipvRemote.Protocols.Abstractions;
using System.Collections.Concurrent;

namespace LoipvRemote.UseCases.Sessions;

public enum SessionStartResult
{
    Started,
    InitializationFailed,
    ConnectionFailed
}

/// <summary>Owns the protocol lifecycle transitions used by application use cases.</summary>
public sealed class SessionLifecycleCoordinator
{
    private readonly ConcurrentDictionary<IProtocolSession, byte> _activeSessions = new();

    public int ActiveSessionCount => _activeSessions.Count;

    public SessionStartResult Start(IProtocolSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.Initialize())
        {
            session.Close();
            return SessionStartResult.InitializationFailed;
        }

        if (!session.Connect())
        {
            session.Close();
            return SessionStartResult.ConnectionFailed;
        }

        _activeSessions.TryAdd(session, 0);
        return SessionStartResult.Started;
    }

    public async ValueTask<SessionStartResult> StartAsync(
        IProtocolSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!await session.InitializeAsync(cancellationToken).ConfigureAwait(false))
        {
            await session.CloseAsync(cancellationToken).ConfigureAwait(false);
            return SessionStartResult.InitializationFailed;
        }

        if (!await session.ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            await session.CloseAsync(cancellationToken).ConfigureAwait(false);
            return SessionStartResult.ConnectionFailed;
        }

        _activeSessions.TryAdd(session, 0);
        return SessionStartResult.Started;
    }

    public void Stop(IProtocolSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _activeSessions.TryRemove(session, out _);
        session.Disconnect();
    }

    public async ValueTask StopAsync(IProtocolSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        _activeSessions.TryRemove(session, out _);
        await session.DisconnectAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAllAsync(CancellationToken cancellationToken)
    {
        foreach (IProtocolSession session in _activeSessions.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_activeSessions.TryRemove(session, out _))
                continue;

            try
            {
                    await session.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await session.CloseAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    session.Dispose();
                }
            }
        }

    }
}
