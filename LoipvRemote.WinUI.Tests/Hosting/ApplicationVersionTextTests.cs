using LoipvRemote.WinUI.Hosting;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class ApplicationVersionTextTests
{
    [Test]
    public void WelcomeVersionUsesTheCompleteFourPartApplicationVersion()
    {
        Assert.That(
            ApplicationVersionText.Format(new Version(3, 0, 2, 43)),
            Is.EqualTo("Version 3.0.2.43"));
    }

    [Test]
    public void WelcomeVersionHasASafeFallbackWhenAssemblyVersionIsUnavailable()
    {
        Assert.That(ApplicationVersionText.Format(null), Is.EqualTo("Version unavailable"));
    }
}
