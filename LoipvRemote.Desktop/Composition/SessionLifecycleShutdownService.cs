using LoipvRemote.UseCases.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoipvRemote.Desktop.Composition;

internal sealed class SessionLifecycleShutdownService(
    SessionLifecycleCoordinator lifecycleCoordinator,
    ILogger<SessionLifecycleShutdownService> logger) : IHostedService
{
    private static readonly Action<ILogger, int, Exception?> ClosingSessions =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, nameof(ClosingSessions)),
            "Closing {ActiveSessionCount} active connection session(s).");

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        int activeSessions = lifecycleCoordinator.ActiveSessionCount;
        if (activeSessions == 0)
            return;

        ClosingSessions(logger, activeSessions, null);
        await lifecycleCoordinator.StopAllAsync(cancellationToken).ConfigureAwait(false);
    }
}
