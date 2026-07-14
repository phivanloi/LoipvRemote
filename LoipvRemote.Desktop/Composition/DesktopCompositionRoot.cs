using LoipvRemote.UseCases.Sessions;

namespace LoipvRemote.Desktop.Composition;

/// <summary>
/// Creates the application services consumed by the WinForms desktop host.
/// </summary>
public sealed class DesktopCompositionRoot
{
    public DesktopCompositionRoot(
        SessionLifecycleCoordinator sessionLifecycleCoordinator,
        ConnectionSessionOrchestrator sessionOrchestrator)
    {
        ArgumentNullException.ThrowIfNull(sessionLifecycleCoordinator);
        ArgumentNullException.ThrowIfNull(sessionOrchestrator);

        SessionLifecycleCoordinator = sessionLifecycleCoordinator;
        SessionOrchestrator = sessionOrchestrator;
    }

    public SessionLifecycleCoordinator SessionLifecycleCoordinator { get; }

    public ConnectionSessionOrchestrator SessionOrchestrator { get; }
}
