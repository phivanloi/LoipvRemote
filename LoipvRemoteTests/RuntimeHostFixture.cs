using LoipvRemote.App;
using LoipvRemote.App.Composition;
using LoipvRemote.Desktop.Composition;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace LoipvRemoteTests;

[SetUpFixture]
public sealed class RuntimeHostFixture
{
    private IHost? _host;

    [OneTimeSetUp]
    public async Task StartHostAsync()
    {
        _host = DesktopApplicationHost.Create([], DesktopServiceRegistration.Register);
        Runtime.Initialize(_host.Services);
        await _host.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopHostAsync()
    {
        if (_host is null)
            return;

        try
        {
            await _host.StopAsync();
        }
        finally
        {
            Runtime.Uninitialize();
            _host.Dispose();
        }
    }
}
