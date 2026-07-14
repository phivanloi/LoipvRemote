using LoipvRemote.UseCases.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoipvRemote.Desktop.Composition;

internal sealed class SessionLifecycleShutdownService(
    SessionLifecycleCoordinator lifecycleCoordinator,
    ILogger<SessionLifecycleShutdownService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        int activeSessions = lifecycleCoordinator.ActiveSessionCount;
        if (activeSessions == 0)
            return;

        logger.LogInformation("Closing {ActiveSessionCount} active connection session(s).", activeSessions);
        await lifecycleCoordinator.StopAllAsync(cancellationToken).ConfigureAwait(false);
    }
}
