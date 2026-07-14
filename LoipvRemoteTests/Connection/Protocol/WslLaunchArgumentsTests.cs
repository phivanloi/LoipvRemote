using LoipvRemote.Protocols.ExternalApps;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

[TestFixture]
public sealed class WslLaunchArgumentsTests
{
    [Test]
    public void BuildsDistributionAndUserAsSeparateArguments()
    {
        Assert.That(WslLaunchArguments.Build("Ubuntu-24.04", "developer"),
            Is.EqualTo(new[] { "-d", "Ubuntu-24.04", "-u", "developer" }));
    }

    [Test]
    public void LocalhostUsesDefaultDistribution() =>
        Assert.That(WslLaunchArguments.Build("localhost", "root"),
            Is.EqualTo(new[] { "-u", "root" }));
}
