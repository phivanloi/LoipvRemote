using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.UseCases.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoipvRemote.Desktop.Composition;

/// <summary>Creates the single Generic Host used by the WinForms desktop application.</summary>
public static class DesktopApplicationHost
{
    public static IHost Create(
        string[] args,
        Action<IServiceCollection> configureApplicationServices)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configureApplicationServices);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();

        builder.Services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(10));
        builder.Services.AddSingleton<SessionLifecycleCoordinator>();
        builder.Services.AddSingleton<ConnectionSessionOrchestrator>();
        builder.Services.AddSingleton<DesktopCompositionRoot>();
        builder.Services.AddHostedService<SessionLifecycleShutdownService>();

        configureApplicationServices(builder.Services);
        ValidateRequiredRegistrations(builder.Services);

        return builder.Build();
    }

    private static void ValidateRequiredRegistrations(IServiceCollection services)
    {
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IProtocolFactory)))
            throw new InvalidOperationException($"{nameof(IProtocolFactory)} must be registered by the application composition callback.");
    }
}
