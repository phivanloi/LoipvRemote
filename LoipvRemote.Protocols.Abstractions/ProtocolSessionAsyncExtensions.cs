namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Runs a migrated async session without forcing legacy sessions to implement proxy-hostile default interface methods.</summary>
public static class ProtocolSessionAsyncExtensions
{
    public static ValueTask<bool> InitializeAsync(this IProtocolSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        return session is IAsyncProtocolSession asynchronous
            ? asynchronous.InitializeAsync(cancellationToken)
            : ValueTask.FromResult(session.Initialize());
    }

    public static ValueTask<bool> ConnectAsync(this IProtocolSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        return session is IAsyncProtocolSession asynchronous
            ? asynchronous.ConnectAsync(cancellationToken)
            : ValueTask.FromResult(session.Connect());
    }

    public static ValueTask DisconnectAsync(this IProtocolSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        if (session is IAsyncProtocolSession asynchronous)
            return asynchronous.DisconnectAsync(cancellationToken);
        session.Disconnect();
        return ValueTask.CompletedTask;
    }

    public static ValueTask CloseAsync(this IProtocolSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        if (session is IAsyncProtocolSession asynchronous)
            return asynchronous.CloseAsync(cancellationToken);
        session.Close();
        return ValueTask.CompletedTask;
    }

    public static ValueTask DisposeAsyncSafe(this IProtocolSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session is IAsyncDisposable asynchronous)
            return asynchronous.DisposeAsync();
        session.Dispose();
        return ValueTask.CompletedTask;
    }
}
