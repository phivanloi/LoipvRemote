using LoipvRemote.UseCases.Sessions;
using LoipvRemote.Infrastructure.Windows.ApplicationIdentity;
using LoipvRemote.Infrastructure.Windows.WindowActivation;
using Microsoft.Extensions.DependencyInjection;

namespace LoipvRemote.Desktop.Composition;

/// <summary>
/// Registers services owned by the desktop host itself, including protocol factories
/// and the session lifecycle. Application-specific stores and UI services are added
/// by the executable composition callback.
/// </summary>
public static class DesktopHostServiceRegistration
{
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IApplicationIdentityService, WindowsApplicationIdentityService>();
        services.AddSingleton<IWindowActivationService, WindowsWindowActivationService>();
        ProtocolServiceRegistration.Register(services);
        services.AddSingleton<SessionLifecycleCoordinator>();
        services.AddSingleton<ConnectionSessionOrchestrator>();
        services.AddSingleton<DesktopCompositionRoot>();
        services.AddHostedService<SessionLifecycleShutdownService>();
    }
}
