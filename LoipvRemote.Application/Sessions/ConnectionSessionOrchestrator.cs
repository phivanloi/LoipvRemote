using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Events;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.Domain.Validation;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.UseCases.Sessions;

public enum ConnectionSessionStartStatus
{
    Started,
    InvalidDefinition,
    ProtocolUnavailable,
    InitializationFailed,
    ConnectionFailed
}

/// <summary>Application use case that owns validation, session creation, and failed-session cleanup.</summary>
public sealed class ConnectionSessionOrchestrator(
    IProtocolFactory protocolFactory,
    SessionLifecycleCoordinator lifecycleCoordinator)
{
    private readonly IProtocolFactory _protocolFactory = protocolFactory ?? throw new ArgumentNullException(nameof(protocolFactory));
    private readonly SessionLifecycleCoordinator _lifecycleCoordinator = lifecycleCoordinator ?? throw new ArgumentNullException(nameof(lifecycleCoordinator));

    public event Action<ConnectionSessionStateChanged>? StateChanged;

    public async ValueTask<ConnectionSessionStartOutcome> StartAsync(
        ConnectionDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!IsValid(definition))
        {
            PublishState(definition.Id, ProtocolSessionState.Faulted);
            return new ConnectionSessionStartOutcome(ConnectionSessionStartStatus.InvalidDefinition, null);
        }

        IProtocolSession session;
        try
        {
            session = _protocolFactory.Create(definition);
        }
        catch (NotSupportedException)
        {
            PublishState(definition.Id, ProtocolSessionState.Faulted);
            return new ConnectionSessionStartOutcome(ConnectionSessionStartStatus.ProtocolUnavailable, null);
        }

        return await StartValidatedAsync(definition, session, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts a session already created and bound by a host adapter.
    /// </summary>
    public async ValueTask<ConnectionSessionStartOutcome> StartAsync(
        ConnectionDefinition definition,
        IProtocolSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(session);

        if (!IsValid(definition))
        {
            PublishState(definition.Id, ProtocolSessionState.Faulted);
            return new ConnectionSessionStartOutcome(ConnectionSessionStartStatus.InvalidDefinition, null);
        }

        return await StartValidatedAsync(definition, session, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ConnectionSessionStartOutcome> StartValidatedAsync(
        ConnectionDefinition definition,
        IProtocolSession session,
        CancellationToken cancellationToken)
    {
        SessionStartResult result;
        try
        {
            result = await _lifecycleCoordinator
                .StartAsync(session, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        if (result == SessionStartResult.Started)
        {
            PublishState(definition.Id, session.State);
            return new ConnectionSessionStartOutcome(ConnectionSessionStartStatus.Started, session);
        }

        await session.DisposeAsync().ConfigureAwait(false);
        PublishState(definition.Id, session.State);
        return new ConnectionSessionStartOutcome(
            result == SessionStartResult.InitializationFailed
                ? ConnectionSessionStartStatus.InitializationFailed
                : ConnectionSessionStartStatus.ConnectionFailed,
            null);
    }

    private static bool IsValid(ConnectionDefinition definition)
    {
        try
        {
            ConnectionDefinitionValidator.Validate(definition);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void PublishState(Guid connectionId, ProtocolSessionState state) =>
        StateChanged?.Invoke(new ConnectionSessionStateChanged(connectionId, state, DateTimeOffset.UtcNow));

    public async ValueTask StopAsync(IProtocolSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        try
        {
            await _lifecycleCoordinator.StopAsync(session, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }
}

public sealed record ConnectionSessionStartOutcome(
    ConnectionSessionStartStatus Status,
    IProtocolSession? Session)
{
    public bool IsStarted => Status == ConnectionSessionStartStatus.Started;
}
