using LoipvRemote.Desktop.Composition;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.UseCases.Sessions;
using NUnit.Framework;
using NSubstitute;

namespace LoipvRemoteTests.Desktop.Composition;

public class DesktopCompositionRootTests
{
    [Test]
    public void CreatesOneLifecycleCoordinatorForTheDesktopSessionOrchestrator()
    {
        var lifecycleCoordinator = new SessionLifecycleCoordinator();
        var sessionOrchestrator = new ConnectionSessionOrchestrator(
            Substitute.For<IProtocolFactory>(),
            lifecycleCoordinator);
        var root = new DesktopCompositionRoot(lifecycleCoordinator, sessionOrchestrator);

        Assert.That(root.SessionOrchestrator, Is.Not.Null);
        Assert.That(root.SessionLifecycleCoordinator, Is.Not.Null);
    }
}
