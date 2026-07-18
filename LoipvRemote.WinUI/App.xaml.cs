using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using LoipvRemote.WinUI.Services;
using LoipvRemote.WinUI.Sessions;
using LoipvRemote.Infrastructure.Windows.Dpapi;
using LoipvRemote.Infrastructure.Windows.Com;
using LoipvRemote.Infrastructure.Persistence;
using LoipvRemote.Application.Configuration;
using LoipvRemote.Application.Credentials;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.Vnc;

namespace LoipvRemote.WinUI;

public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly IHost _host;
    private Window? _window;

    public App()
    {
        InitializeComponent();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(static services =>
            {
                services.AddSingleton<IConnectionDefinitionStoreFactory, ConnectionDefinitionStoreFactory>();
                services.AddSingleton<ConnectionStoreConfigurationService>();
                services.AddSingleton<ConnectionStoreSettingsRepository>();
                services.AddSingleton<ConnectionTreeViewStateRepository>();
                services.AddSingleton<ConnectionCatalog>();
                services.AddSingleton<IUserSecretProtector, WindowsDpapiSecretProtector>();
                services.AddSingleton<DpapiStringSecretStore>();
                services.AddSingleton<IStringSecretStore>(provider => provider.GetRequiredService<DpapiStringSecretStore>());
                services.AddSingleton<ILocalCredentialStore, DpapiLocalCredentialStore>();
                services.AddSingleton<ConnectionOptionsEditor>();
                services.AddSingleton<IConnectionSecretResolver, DpapiConnectionSecretResolver>();
                services.AddSingleton<IVncClientFactory, NativeVncClientFactory>();
                services.AddSingleton<IRdpClientFactory, WindowsRdpClientFactory>();
                services.AddSingleton<IWinUIProtocolSessionFactory, WinUIProtocolSessionFactory>();
                services.AddSingleton<RemoteSessionWorkspace>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        EnsureMainWindow();
    }

    private void EnsureMainWindow()
    {
        if (_window is not null)
            return;

        try
        {
            _window = _host.Services.GetRequiredService<MainWindow>();
            _window.Activate();
            _ = StartBackgroundServicesAsync();
        }
        catch (Exception exception)
        {
            // WinUI turns a startup exception into a native XAML crash.  Write
            // the original managed exception to stderr so CI and installer
            // smoke tests preserve the actionable cause.
            Console.Error.WriteLine(exception);
            throw;
        }
    }

    private async Task StartBackgroundServicesAsync()
    {
        try
        {
            await _host.StartAsync();
            await _host.Services.GetRequiredService<ILocalCredentialStore>().ListAsync();
        }
        catch (Exception exception)
        {
            // The shell is already usable at this point. Background initialization
            // must never prevent it from creating a window; the affected feature
            // reports its own error when the user opens it.
            Console.Error.WriteLine(exception);
        }
    }
}
