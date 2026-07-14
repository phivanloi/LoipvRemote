using System.Threading;
using LoipvRemote.Infrastructure.Windows.Com;
using LoipvRemote.Protocols.Abstractions;
using NUnit.Framework;

namespace LoipvRemoteTests.Infrastructure.Windows.Com;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class RdpActiveXRuntimeTests
{
    [Test]
    public void InstalledRdpControl_CanBeCreatedAndInitialized()
    {
        Assert.That(RdpActiveXRuntime.IsSupported(RdpVersion.Rdc10), Is.True);

        using RdpActiveXRuntime runtime = new(RdpVersion.Rdc10);
        runtime.Initialize();

        Assert.That(runtime.ClientVersion, Is.GreaterThan(new Version(0, 0)));
    }
}
