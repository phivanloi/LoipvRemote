using LoipvRemote.App;
using LoipvRemote.App.Composition;
using LoipvRemote.Desktop.Composition;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;

namespace LoipvRemoteTests;

[SetUpFixture]
public sealed class RuntimeHostFixture
{
    private IHost? _host;
    public static IServiceProvider Services => Instance._host?.Services
        ?? throw new InvalidOperationException("The test host has not been started.");

    private static RuntimeHostFixture Instance { get; } = new();

    [OneTimeSetUp]
    public async Task StartHostAsync()
    {
        Instance._host = DesktopApplicationHost.Create([], ApplicationServiceRegistration.Register);
        _host = Instance._host;
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
            _host.Dispose();
            Instance._host = null;
        }
    }
}
