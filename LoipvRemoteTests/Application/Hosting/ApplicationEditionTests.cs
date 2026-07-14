using LoipvRemote.UseCases.Hosting;
using NUnit.Framework;

namespace LoipvRemoteTests.UseCases.Hosting;

public class ApplicationEditionTests
{
    [Test]
    public void DefaultBuildIsNotPortable()
    {
        Assert.That(ApplicationEdition.IsPortable, Is.False);
    }
}
