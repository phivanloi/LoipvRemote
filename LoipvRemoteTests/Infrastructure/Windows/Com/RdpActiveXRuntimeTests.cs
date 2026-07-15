using System.Threading;
using LoipvRemote.Infrastructure.Windows.Com;
using LoipvRemote.Protocols.Abstractions;
using NUnit.Framework;
using System.Windows.Forms;

namespace LoipvRemoteTests.Infrastructure.Windows.Com;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class RdpActiveXRuntimeTests
{
    [TestCase(RdpVersion.Rdc7, "AxMsRdpClient7NotSafeForScripting")]
    [TestCase(RdpVersion.Rdc10, "AxMsRdpClient10NotSafeForScripting")]
    [TestCase(RdpVersion.Rdc11, "AxMsRdpClient11NotSafeForScripting")]
    public void RequestedVersionCreatesTheMatchingActiveXControl(RdpVersion version, string expectedType)
    {
        using RdpActiveXRuntime runtime = new(version);

        Assert.That(runtime.Control.GetType().Name, Is.EqualTo(expectedType));
    }

    [Test]
    public void InstalledRdpControl_CanBeCreatedAndInitialized()
    {
        Assert.That(RdpActiveXRuntime.IsSupported(RdpVersion.Rdc10), Is.True);

        using Panel host = new();
        _ = host.Handle;
        using RdpActiveXRuntime runtime = new(RdpVersion.Rdc10);
        Assert.That(runtime.AttachTo(host.Handle, TimeSpan.FromSeconds(1)), Is.True);
        runtime.Initialize();

        Assert.Multiple(() =>
        {
            Assert.That(runtime.ClientVersion, Is.GreaterThan(new Version(0, 0)));
            Assert.That(runtime.Control.Parent, Is.SameAs(host));
            Assert.That(runtime.Control.Dock, Is.EqualTo(DockStyle.Fill));
        });
    }
}
