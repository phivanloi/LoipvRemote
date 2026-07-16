using System.Reflection;
using LoipvRemote.App;
using NUnit.Framework;

namespace LoipvRemoteTests.App;

[TestFixture]
public sealed class BuildMetadataTests
{
    [Test]
    public void DesktopAssemblyUsesNumericInformationalVersionWithoutSourceMetadata()
    {
        Assembly desktopAssembly = typeof(ProgramRoot).Assembly;
        string informationalVersion = desktopAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? string.Empty;

        Assert.That(informationalVersion, Does.Match(@"^\d+\.\d+\.\d+\.\d+$"));
        Assert.That(informationalVersion, Does.Not.Contain('+'));
        Assert.That(informationalVersion, Does.Not.Contain(' '));
    }
}
