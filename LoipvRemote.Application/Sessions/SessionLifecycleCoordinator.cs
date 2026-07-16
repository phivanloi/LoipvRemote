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

    public async ValueTask<SessionStartResult> StartAsync(
        IProtocolSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        bool connecting = false;
        try
        {
            if (!await session.InitializeAsync(cancellationToken).ConfigureAwait(false))
            {
                await CloseAfterFailedStartAsync(session).ConfigureAwait(false);
                return SessionStartResult.InitializationFailed;
            }

            connecting = true;
            if (!await session.ConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                await CloseAfterFailedStartAsync(session).ConfigureAwait(false);
                return SessionStartResult.ConnectionFailed;
            }

            _activeSessions.TryAdd(session, 0);
            return SessionStartResult.Started;
        }
        catch (OperationCanceledException)
        {
            await CloseAfterFailedStartAsync(session).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await CloseAfterFailedStartAsync(session).ConfigureAwait(false);
            return connecting
                ? SessionStartResult.ConnectionFailed
                : SessionStartResult.InitializationFailed;
        }
    }

    public async ValueTask StopAsync(IProtocolSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        _activeSessions.TryRemove(session, out _);
        await session.DisconnectAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAllAsync(CancellationToken cancellationToken)
    {
        List<Exception> failures = [];
        foreach (IProtocolSession session in _activeSessions.Keys)
        {
            if (!_activeSessions.TryRemove(session, out _))
                continue;

            try
            {
                await session.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Continue best-effort shutdown with an uncancelled close/dispose.
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            try
            {
                await session.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            try
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
            throw new AggregateException("One or more protocol sessions failed during shutdown.", failures);
    }

    private static async ValueTask CloseAfterFailedStartAsync(IProtocolSession session)
    {
        try
        {
            await session.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Preserve the lifecycle result/original cancellation while still
            // allowing the caller to dispose the failed session.
        }
    }
}
