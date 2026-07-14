using System;
using System.Threading.Tasks;
using LoipvRemote.Protocols.Vnc;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

public class VncEndpointProbeTests
{
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(65536)]
    public void RejectsInvalidPortsBeforeOpeningASocket(int port)
    {
        var probe = new VncEndpointProbe();

        Assert.That(
            async () => await probe.ProbeAsync("host.example", port, TimeSpan.FromSeconds(1)),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }
}
